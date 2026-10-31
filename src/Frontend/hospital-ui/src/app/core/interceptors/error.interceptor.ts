import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { ApiResponse } from '../models/api-response.model';
import { NotificationService } from '../services/notification.service';

/** Endpoints whose failures are handled by the calling code (login form, session restore). */
const SILENT_ENDPOINTS = ['/auth/login', '/auth/refresh', '/auth/logout'];

/**
 * Central HTTP error handling. Validation (400), business-rule (422) and conflict (409) errors are
 * re-thrown untouched so pages can show them in context; everything else gets a user-facing message.
 * Registered before authInterceptor so it only sees errors after any refresh-and-retry attempt.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || SILENT_ENDPOINTS.some((e) => request.url.endsWith(e))) {
        return throwError(() => error);
      }

      const body = error.error as ApiResponse<unknown> | null;
      const message = body?.message || 'An unexpected error occurred.';

      switch (error.status) {
        case 0:
          notifications.error('Cannot reach the server. Check your network connection.');
          break;
        case 400:
        case 409:
        case 422:
          break;
        case 401:
          auth.clearSession();
          notifications.error('Your session has expired. Please sign in again.');
          void router.navigate(['/login']);
          break;
        case 403:
          notifications.error('You do not have permission to perform this action.');
          break;
        case 429:
          notifications.error('Too many requests. Please wait a moment and try again.');
          break;
        default:
          notifications.error(body?.traceId ? `${message} (Trace: ${body.traceId})` : message);
      }

      return throwError(() => error);
    }),
  );
};
