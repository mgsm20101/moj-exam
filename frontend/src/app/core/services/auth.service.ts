import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResponse {
  token: string;
  refreshToken: string;
  userName: string;
  roles: string[];
}

// Known Phase 0 tradeoff: localStorage is readable by any injected script (XSS risk). Both the
// access token and the (rotating) refresh token live here for now; revisit before production
// (target: httpOnly refresh cookie + in-memory access token) as part of a security hardening pass.
const TOKEN_KEY = 'auth_token';
const REFRESH_KEY = 'refresh_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly http: HttpClient) {}

  login(userName: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiBaseUrl}/auth/login`, { userName, password })
      .pipe(tap(response => this.storeSession(response)));
  }

  /** Exchanges the stored refresh token for a fresh access/refresh pair (silent renewal). */
  refreshToken(): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiBaseUrl}/auth/refresh`, { refreshToken: this.getRefreshToken() })
      .pipe(tap(response => this.storeSession(response)));
  }

  /** Revokes the refresh token server-side (best-effort) and clears the local session. */
  logout(): void {
    const refreshToken = this.getRefreshToken();
    this.clearSession();
    if (refreshToken) {
      // Fire-and-forget: local sign-out must not depend on the network round-trip succeeding.
      this.http.post(`${environment.apiBaseUrl}/auth/logout`, { refreshToken }).subscribe({ error: () => {} });
    }
  }

  /** Wipes tokens without a server call — used by the interceptor when a refresh has already failed. */
  clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  }

  isAuthenticated(): boolean {
    // A present refresh token means the session can be silently renewed; a present (possibly still
    // valid) access token also counts. The interceptor is the safety net that forces logout on a
    // 401 that can't be refreshed.
    return !!this.getRefreshToken() || !!this.getToken();
  }

  private storeSession(response: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(REFRESH_KEY, response.refreshToken);
  }
}
