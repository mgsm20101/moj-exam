import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService, LoginResponse } from '../services/auth.service';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: router }
      ]
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('adds an Authorization header when a token exists', () => {
    spyOn(authService, 'getToken').and.returnValue('jwt-token');

    httpClient.get('/api/anything').subscribe();

    const req = httpMock.expectOne('/api/anything');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    req.flush({});
  });

  it('does not add an Authorization header when no token exists', () => {
    spyOn(authService, 'getToken').and.returnValue(null);

    httpClient.get('/api/anything').subscribe();

    const req = httpMock.expectOne('/api/anything');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('on 401 refreshes the token and retries the request with the new token', () => {
    spyOn(authService, 'getToken').and.returnValue('expired-token');
    spyOn(authService, 'getRefreshToken').and.returnValue('refresh-token');
    const refreshed: LoginResponse = { token: 'new-token', refreshToken: 'new-refresh', userName: 'admin', roles: ['Admin'] };
    const refreshSpy = spyOn(authService, 'refreshToken').and.returnValue(of(refreshed));

    let body: unknown = null;
    httpClient.get('/api/admin/data').subscribe(r => (body = r));

    const first = httpMock.expectOne('/api/admin/data');
    first.flush({ error: 'expired' }, { status: 401, statusText: 'Unauthorized' });

    const retry = httpMock.expectOne('/api/admin/data');
    expect(refreshSpy).toHaveBeenCalled();
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    retry.flush({ ok: true });

    expect(body).toEqual({ ok: true });
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('on 401 with a failing refresh clears the session and redirects to login', () => {
    spyOn(authService, 'getToken').and.returnValue('expired-token');
    spyOn(authService, 'getRefreshToken').and.returnValue('refresh-token');
    spyOn(authService, 'refreshToken').and.returnValue(
      throwError(() => new Error('refresh failed'))
    );
    const clearSpy = spyOn(authService, 'clearSession');

    let errored = false;
    httpClient.get('/api/admin/data').subscribe({ error: () => (errored = true) });

    const first = httpMock.expectOne('/api/admin/data');
    first.flush({ error: 'expired' }, { status: 401, statusText: 'Unauthorized' });

    expect(clearSpy).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { sessionExpired: '1' } });
    expect(errored).toBeTrue();
  });

  it('does not attempt a refresh for candidate exam calls', () => {
    spyOn(authService, 'getToken').and.returnValue('expired-token');
    spyOn(authService, 'getRefreshToken').and.returnValue('refresh-token');
    const refreshSpy = spyOn(authService, 'refreshToken');

    let errored = false;
    httpClient
      .get('/api/exam/11111111-1111-1111-1111-111111111111/attempt/state')
      .subscribe({ error: () => (errored = true) });

    const req = httpMock.expectOne(r => r.url.includes('/attempt/state'));
    req.flush({ error: 'expired' }, { status: 401, statusText: 'Unauthorized' });

    expect(refreshSpy).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(errored).toBeTrue();
  });
});
