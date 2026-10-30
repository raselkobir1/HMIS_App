import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AuthUser, LoginRequest, LoginResponse } from './auth.models';

/**
 * Holds the current session. The access token lives in memory only; the refresh token is an
 * HttpOnly cookie managed by the API (wired up in the Identity module).
 * Permission checks here only shape the UI — the backend enforces every permission.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly userSignal = signal<AuthUser | null>(null);

  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== null);
  readonly permissions = computed(() => new Set(this.userSignal()?.permissions ?? []));

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  login(request: LoginRequest): Observable<AuthUser> {
    return this.http
      .post<ApiResponse<LoginResponse>>(`${this.baseUrl}/login`, request, { withCredentials: true })
      .pipe(
        map((response) => response.data as LoginResponse),
        tap((data) => this.setSession(data)),
        map((data) => data.user),
      );
  }

  logout(): void {
    this.http.post(`${this.baseUrl}/logout`, {}, { withCredentials: true }).subscribe({ error: () => undefined });
    this.clearSession();
    void this.router.navigate(['/login']);
  }

  hasPermission(permission: string): boolean {
    return this.permissions().has(permission);
  }

  hasAnyPermission(permissions: readonly string[]): boolean {
    return permissions.length === 0 || permissions.some((p) => this.permissions().has(p));
  }

  clearSession(): void {
    this.accessTokenSignal.set(null);
    this.userSignal.set(null);
  }

  private setSession(data: LoginResponse): void {
    this.accessTokenSignal.set(data.accessToken);
    this.userSignal.set(data.user);
  }
}
