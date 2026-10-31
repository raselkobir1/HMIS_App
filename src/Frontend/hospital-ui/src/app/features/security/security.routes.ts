import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { SecurityPermissions } from './models/security.models';

export default [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  {
    path: 'users',
    title: 'Users · HMIS',
    canActivate: [permissionGuard(SecurityPermissions.usersView)],
    loadComponent: () => import('./pages/users/users-list.page').then((m) => m.UsersListPage),
  },
  {
    path: 'roles',
    title: 'Roles & Permissions · HMIS',
    canActivate: [permissionGuard(SecurityPermissions.rolesView)],
    loadComponent: () => import('./pages/roles/roles-list.page').then((m) => m.RolesListPage),
  },
  {
    path: 'roles/:id/permissions',
    title: 'Role Permissions · HMIS',
    canActivate: [permissionGuard(SecurityPermissions.rolesView)],
    loadComponent: () => import('./pages/roles/role-permissions.page').then((m) => m.RolePermissionsPage),
  },
  {
    path: 'login-history',
    title: 'Login History · HMIS',
    canActivate: [permissionGuard(SecurityPermissions.loginHistoryView)],
    loadComponent: () => import('./pages/login-history/login-history.page').then((m) => m.LoginHistoryPage),
  },
] satisfies Routes;
