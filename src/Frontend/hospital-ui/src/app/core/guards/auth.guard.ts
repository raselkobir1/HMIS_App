import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../auth/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** While a password change is pending the user may only visit the change-password page. */
export const passwordChangeGuard: CanActivateChildFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.mustChangePassword() && !state.url.startsWith('/change-password')) {
    return inject(Router).createUrlTree(['/change-password']);
  }
  return true;
};

/** Keeps signed-in users away from the login page. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/dashboard']) : true;
