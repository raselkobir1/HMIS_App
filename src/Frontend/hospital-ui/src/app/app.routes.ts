import { Routes } from '@angular/router';

import { authGuard, guestGuard, passwordChangeGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./layouts/authentication-layout/authentication-layout').then((m) => m.AuthenticationLayout),
    children: [
      {
        path: '',
        title: 'Sign in · HMIS',
        loadComponent: () => import('./features/auth/login/login.page').then((m) => m.LoginPage),
      },
    ],
  },
  {
    path: '',
    canActivate: [authGuard],
    canActivateChild: [passwordChangeGuard],
    loadComponent: () => import('./layouts/admin-layout/admin-layout').then((m) => m.AdminLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · HMIS',
        loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'change-password',
        title: 'Change Password · HMIS',
        loadComponent: () =>
          import('./features/auth/change-password/change-password.page').then((m) => m.ChangePasswordPage),
      },
      { path: 'security', loadChildren: () => import('./features/security/security.routes') },
    ],
  },
  { path: '**', redirectTo: '' },
];
