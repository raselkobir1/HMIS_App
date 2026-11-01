import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { AdminPermissions } from './administration.models';

export default [
  { path: '', pathMatch: 'full', redirectTo: 'hospital-profile' },
  {
    path: 'hospital-profile',
    title: 'Hospital Profile · HMIS',
    canActivate: [permissionGuard(AdminPermissions.profileView)],
    loadComponent: () => import('./pages/hospital-profile.page').then((m) => m.HospitalProfilePage),
  },
  {
    path: 'departments',
    title: 'Departments · HMIS',
    canActivate: [permissionGuard(AdminPermissions.departmentsView)],
    loadComponent: () => import('./pages/departments.page').then((m) => m.DepartmentsPage),
  },
  {
    path: 'designations',
    title: 'Designations · HMIS',
    canActivate: [permissionGuard(AdminPermissions.designationsView)],
    loadComponent: () => import('./pages/designations.page').then((m) => m.DesignationsPage),
  },
  {
    path: 'settings',
    title: 'Settings & Numbering · HMIS',
    canActivate: [permissionGuard(AdminPermissions.settingsManage)],
    loadComponent: () => import('./pages/system-settings.page').then((m) => m.SystemSettingsPage),
  },
  {
    path: 'notifications',
    title: 'Notification Settings · HMIS',
    canActivate: [permissionGuard('Notifications.Settings.Manage')],
    loadComponent: () => import('./pages/notification-settings.page').then((m) => m.NotificationSettingsPage),
  },
] satisfies Routes;
