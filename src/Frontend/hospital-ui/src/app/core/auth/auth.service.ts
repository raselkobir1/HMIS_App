import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, map, Observable, of, shareReplay } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AuthUser, ChangePasswordRequest, LoginRequest, LoginResponse } from './auth.models';

/**
 * Holds the current session. The access token lives in memory only; the refresh token is an
 * HttpOnly cookie that the browser sends to /api/v1/auth automatically.
 * Permission checks here only shape the UI — the backend enforces every permission.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly userSignal = signal<AuthUser | null>(null);
  private refreshInFlight$: Observable<string | null> | null = null;

  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== null);
  readonly mustChangePassword = computed(() => this.userSignal()?.mustChangePassword ?? false);
  readonly permissions = computed(() => new Set(this.userSignal()?.permissions ?? []));

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  login(request: LoginRequest): Observable<AuthUser> {
    return this.http
      .post<ApiResponse<LoginResponse>>(`${this.baseUrl}/login`, request)
      .pipe(map((response) => this.setSession(response.data as LoginResponse)));
  }

  changePassword(request: ChangePasswordRequest): Observable<AuthUser> {
    return this.http
      .post<ApiResponse<LoginResponse>>(`${this.baseUrl}/change-password`, request)
      .pipe(map((response) => this.setSession(response.data as LoginResponse)));
  }

  /**
   * Exchanges the refresh cookie for a new access token. Concurrent callers share one request.
   * Emits the new token, or null when the session can no longer be refreshed.
   */
  refresh(): Observable<string | null> {
    this.refreshInFlight$ ??= this.http.post<ApiResponse<LoginResponse>>(`${this.baseUrl}/refresh`, {}).pipe(
      map((response) => {
        this.setSession(response.data as LoginResponse);
        return this.accessTokenSignal();
      }),
      catchError(() => {
        this.clearSession();
        return of(null);
      }),
      finalize(() => (this.refreshInFlight$ = null)),
      shareReplay(1),
    );
    return this.refreshInFlight$;
  }

  /** Called once at application start: restores the session from the refresh cookie if one exists. */
  restoreSession(): Observable<unknown> {
    return this.refresh();
  }

  logout(reason?: 'idle'): void {
    // Navigate only after the server has revoked the refresh token and cleared its cookie; otherwise an immediate
    // reload could abort the request and the still-valid cookie would silently sign the user back in.
    const goToLogin = () => void this.router.navigate(['/login'], reason ? { queryParams: { reason } } : undefined);
    // Subscribe before clearing the session so the interceptor still attaches the access token.
    this.http.post(`${this.baseUrl}/logout`, {}).subscribe({ complete: goToLogin, error: goToLogin });
    this.clearSession();
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

  private setSession(data: LoginResponse): AuthUser {
    this.accessTokenSignal.set(data.accessToken);
    this.userSignal.set(data.user);
    return data.user;
  }
}
