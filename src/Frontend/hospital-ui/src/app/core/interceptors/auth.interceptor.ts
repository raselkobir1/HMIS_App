import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

/** Auth endpoints that must never trigger a refresh-and-retry. */
const SESSION_ENDPOINTS = ['/auth/login', '/auth/refresh', '/auth/logout'];

const withToken = (request: HttpRequest<unknown>, token: string | null) =>
  token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

/**
 * Attaches the bearer token to HMIS API requests. On 401 it refreshes the session once
 * (shared across concurrent requests) and retries; if refresh fails the 401 propagates
 * to the error interceptor, which sends the user to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith(environment.apiBaseUrl)) {
    return next(request);
  }

  const auth = inject(AuthService);
  const isSessionEndpoint = SESSION_ENDPOINTS.some((endpoint) => request.url.endsWith(endpoint));

  return next(withToken(request, auth.accessToken)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isSessionEndpoint || !auth.accessToken) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap((token) => (token ? next(withToken(request, token)) : throwError(() => error))),
      );
    }),
  );
};
