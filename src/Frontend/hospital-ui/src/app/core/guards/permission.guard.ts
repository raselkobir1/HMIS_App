import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../auth/auth.service';

/**
 * Route guard: allows navigation when the user holds any of the given permissions.
 * Usage: canActivate: [permissionGuard('Patient.View')]. UI convenience only; the API enforces access.
 */
export const permissionGuard =
  (...permissions: string[]): CanActivateFn =>
  () =>
    inject(AuthService).hasAnyPermission(permissions) ? true : inject(Router).createUrlTree(['/dashboard']);
