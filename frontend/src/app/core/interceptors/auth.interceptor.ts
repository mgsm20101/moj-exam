import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Shared across every request so a burst of concurrent 401s triggers only ONE /auth/refresh call:
// the first request refreshes, the rest park on `refreshedToken$` and replay once the new token lands.
let isRefreshing = false;
const refreshedToken$ = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.getToken();
  const authReq = token ? addToken(req, token) : req;

  return next(authReq).pipe(
    catchError((error: unknown) => {
      const isExpiredAdminCall =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !isAuthEndpoint(req.url) &&
        !isCandidateExamCall(req.url);

      if (isExpiredAdminCall) {
        return handle401(req, next, authService, router);
      }
      return throwError(() => error);
    })
  );
};

function handle401(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router
): Observable<HttpEvent<unknown>> {
  // Nothing to renew with — the session is dead, so bounce to login.
  if (!authService.getRefreshToken()) {
    return forceLogout(authService, router);
  }

  // A refresh is already in flight: wait for it, then replay this request with the new token.
  if (isRefreshing) {
    return refreshedToken$.pipe(
      filter(newToken => newToken !== null),
      take(1),
      switchMap(newToken => next(addToken(req, newToken!)))
    );
  }

  isRefreshing = true;
  refreshedToken$.next(null);

  return authService.refreshToken().pipe(
    switchMap(response => {
      isRefreshing = false;
      refreshedToken$.next(response.token);
      return next(addToken(req, response.token));
    }),
    catchError(() => {
      isRefreshing = false;
      return forceLogout(authService, router);
    })
  );
}

function forceLogout(authService: AuthService, router: Router): Observable<HttpEvent<unknown>> {
  authService.clearSession();
  // Signal the login page so it can show a "session expired, please sign in again" notice.
  router.navigate(['/login'], { queryParams: { sessionExpired: '1' } });
  return throwError(() => new Error('Session expired. Please sign in again.'));
}

function addToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

// Never try to refresh in response to the auth endpoints themselves (avoids an infinite loop).
function isAuthEndpoint(url: string): boolean {
  return url.includes('/auth/login') || url.includes('/auth/refresh') || url.includes('/auth/logout');
}

// Candidate exam calls carry a separate short-lived AttemptToken (see attempt-token.interceptor);
// a 401 there is not an admin-session expiry and must not trigger an admin refresh/logout.
function isCandidateExamCall(url: string): boolean {
  return /\/api\/exam\/[0-9a-fA-F-]+\/attempt/.test(url);
}
