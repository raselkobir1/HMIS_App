import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { NursingPermissions } from './nursing.models';

export default [
  { path: '', pathMatch: 'full', redirectTo: 'ward' },
  {
    path: 'ward',
    title: 'Ward Nursing · HMIS',
    canActivate: [permissionGuard(NursingPermissions.view)],
    loadComponent: () => import('./pages/ward.page').then((m) => m.WardPage),
  },
  {
    path: 'patients/:id',
    title: 'Patient Chart · HMIS',
    canActivate: [permissionGuard(NursingPermissions.view, NursingPermissions.clinicalView)],
    loadComponent: () => import('./pages/patient-chart.page').then((m) => m.PatientChartPage),
  },
  {
    path: 'handover',
    title: 'Shift Handover · HMIS',
    canActivate: [permissionGuard(NursingPermissions.view)],
    loadComponent: () => import('./pages/handover.page').then((m) => m.HandoverPage),
  },
  {
    path: 'roster',
    title: 'Nurse Roster · HMIS',
    canActivate: [permissionGuard(NursingPermissions.view, NursingPermissions.rosterManage)],
    loadComponent: () => import('./pages/roster.page').then((m) => m.RosterPage),
  },
] satisfies Routes;
