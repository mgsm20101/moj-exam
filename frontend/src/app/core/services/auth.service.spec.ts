import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService, LoginResponse } from './auth.service';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const loginResponse: LoginResponse = {
    token: 'jwt-token',
    refreshToken: 'refresh-token',
    userName: 'admin',
    roles: ['Admin']
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('logs in, stores both tokens, and reports authenticated state', () => {
    service.login('admin', 'secret').subscribe(response => {
      expect(response.token).toBe('jwt-token');
      expect(service.isAuthenticated()).toBeTrue();
      expect(localStorage.getItem('auth_token')).toBe('jwt-token');
      expect(localStorage.getItem('refresh_token')).toBe('refresh-token');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(loginResponse);
  });

  it('reports not authenticated when no token is stored', () => {
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('refreshToken() posts the stored refresh token and stores the new pair', () => {
    localStorage.setItem('refresh_token', 'old-refresh');

    service.refreshToken().subscribe(response => {
      expect(response.token).toBe('jwt-token');
      expect(localStorage.getItem('auth_token')).toBe('jwt-token');
      expect(localStorage.getItem('refresh_token')).toBe('refresh-token');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/auth/refresh`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'old-refresh' });
    req.flush(loginResponse);
  });

  it('logout clears both tokens and revokes the refresh token server-side', () => {
    localStorage.setItem('auth_token', 'jwt-token');
    localStorage.setItem('refresh_token', 'refresh-token');

    service.logout();

    expect(localStorage.getItem('auth_token')).toBeNull();
    expect(localStorage.getItem('refresh_token')).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/auth/logout`);
    expect(req.request.body).toEqual({ refreshToken: 'refresh-token' });
    req.flush(null);
  });

  it('logout makes no server call when there is no refresh token', () => {
    localStorage.setItem('auth_token', 'jwt-token');
    service.logout();
    expect(localStorage.getItem('auth_token')).toBeNull();
    // httpMock.verify() in afterEach asserts no outstanding request was made.
  });
});
