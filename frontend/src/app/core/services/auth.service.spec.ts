import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService, LoginResponse } from './auth.service';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

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

  it('logs in, stores the token, and reports authenticated state', () => {
    const mockResponse: LoginResponse = { token: 'jwt-token', userName: 'admin', roles: ['Admin'] };

    service.login('admin', 'secret').subscribe(response => {
      expect(response.token).toBe('jwt-token');
      expect(service.isAuthenticated()).toBeTrue();
      expect(localStorage.getItem('auth_token')).toBe('jwt-token');
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('reports not authenticated when no token is stored', () => {
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('logout clears the stored token', () => {
    localStorage.setItem('auth_token', 'jwt-token');
    service.logout();
    expect(localStorage.getItem('auth_token')).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });
});
