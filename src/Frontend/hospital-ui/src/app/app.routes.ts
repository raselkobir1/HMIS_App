import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './core/guards/auth.guard';

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
    loadComponent: () => import('./layouts/admin-layout/admin-layout').then((m) => m.AdminLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · HMIS',
        loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      // Feature modules are added here as lazy routes when approved, e.g.
      // { path: 'patients', canActivate: [permissionGuard('Patient.View')], loadChildren: () => import('./features/patient/patient.routes') }
    ],
  },
  { path: '**', redirectTo: '' },
];
