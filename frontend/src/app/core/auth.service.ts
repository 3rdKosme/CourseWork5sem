import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthResponse, Role, User } from './models';

const ACCESS_TOKEN_KEY = 'periphshop.accessToken';
const REFRESH_TOKEN_KEY = 'periphshop.refreshToken';
const USER_KEY = 'periphshop.user';

/**
 * Аутентификация и хранение токенов (FR-12 … FR-15).
 * Токены живут в localStorage, состояние пользователя — в signal.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly user = signal<User | null>(readJson<User>(USER_KEY));
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly isStaff = computed(() => {
    const role = this.user()?.role;
    return role === 'Manager' || role === 'Admin';
  });
  readonly isAdmin = computed(() => this.user()?.role === 'Admin');

  get accessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  get refreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  hasRole(...roles: Role[]): boolean {
    const role = this.user()?.role;
    return role !== undefined && roles.includes(role);
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', { email, password })
      .pipe(tap((response) => this.store(response)));
  }

  register(email: string, password: string, fullName: string, phone?: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/register', { email, password, fullName, phone })
      .pipe(tap((response) => this.store(response)));
  }

  /** Обновление пары токенов; вызывается интерсептором при 401. */
  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/refresh', { refreshToken: this.refreshToken ?? '' })
      .pipe(tap((response) => this.store(response)));
  }

  updateUser(user: User): void {
    this.user.set(user);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }

  logout(redirect = true): void {
    const token = this.refreshToken;
    if (token) {
      // Отзыв токена на сервере — «best effort»: локальный выход выполняется в любом случае.
      this.http.post('/api/auth/logout', { refreshToken: token }).subscribe({ error: () => undefined });
    }

    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.user.set(null);

    if (redirect) void this.router.navigate(['/login']);
  }

  private store(response: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    this.updateUser(response.user);
  }
}

function readJson<T>(key: string): T | null {
  const raw = localStorage.getItem(key);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    localStorage.removeItem(key);
    return null;
  }
}
