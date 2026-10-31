import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { DoctorPermissions } from './doctors.models';
import { HrPermissions } from './hr.models';

export default [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  {
    path: 'employees',
    title: 'Employees · HMIS',
    canActivate: [permissionGuard(HrPermissions.employeesView)],
    loadComponent: () => import('./pages/employees.page').then((m) => m.EmployeesPage),
  },
  {
    path: 'doctors',
    title: 'Doctors · HMIS',
    canActivate: [permissionGuard(DoctorPermissions.view)],
    loadComponent: () => import('./pages/doctors.page').then((m) => m.DoctorsPage),
  },
] satisfies Routes;
