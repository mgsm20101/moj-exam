import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResponse {
  token: string;
  userName: string;
  roles: string[];
}

// Known Phase 0 tradeoff: localStorage is readable by any injected script (XSS risk).
// Acceptable short-term given the PRD's short-lived JWT design; revisit before production
// (target: httpOnly refresh cookie + in-memory access token) as part of a security hardening pass.
const TOKEN_KEY = 'auth_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly http: HttpClient) {}

  login(userName: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiBaseUrl}/auth/login`, { userName, password })
      .pipe(tap(response => localStorage.setItem(TOKEN_KEY, response.token)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }
}
