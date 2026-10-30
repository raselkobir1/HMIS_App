import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { ApiResponse } from '../models/api-response.model';
import { NotificationService } from '../services/notification.service';

/**
 * Central HTTP error handling. Validation (400) and business-rule (422) errors are re-thrown
 * untouched so forms can show field errors; everything else gets a user-facing message.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const body = error.error as ApiResponse<unknown> | null;
      const message = body?.message || 'An unexpected error occurred.';

      switch (error.status) {
        case 0:
          notifications.error('Cannot reach the server. Check your network connection.');
          break;
        case 400:
        case 422:
          break;
        case 401:
          if (!request.url.endsWith('/auth/login')) {
            auth.clearSession();
            void router.navigate(['/login']);
          }
          break;
        case 403:
          notifications.error('You do not have permission to perform this action.');
          break;
        default:
          notifications.error(body?.traceId ? `${message} (Trace: ${body.traceId})` : message);
      }

      return throwError(() => error);
    }),
  );
};
