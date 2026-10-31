import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { IpdPermissions } from './ipd.models';

export default [
  { path: '', pathMatch: 'full', redirectTo: 'beds' },
  {
    path: 'beds',
    title: 'Bed Board · HMIS',
    canActivate: [permissionGuard(IpdPermissions.view)],
    loadComponent: () => import('./pages/bed-board.page').then((m) => m.BedBoardPage),
  },
  {
    path: 'admissions',
    title: 'Admissions · HMIS',
    canActivate: [permissionGuard(IpdPermissions.view)],
    loadComponent: () => import('./pages/admissions.page').then((m) => m.AdmissionsPage),
  },
  {
    path: 'admit',
    title: 'Admit Patient · HMIS',
    canActivate: [permissionGuard(IpdPermissions.admit)],
    loadComponent: () => import('./pages/admit.page').then((m) => m.AdmitPage),
  },
  {
    path: 'admissions/:id',
    title: 'Admission · HMIS',
    canActivate: [permissionGuard(IpdPermissions.view)],
    loadComponent: () => import('./pages/admission.page').then((m) => m.AdmissionPage),
  },
  {
    path: 'admissions/:id/summary',
    title: 'Discharge Summary · HMIS',
    canActivate: [permissionGuard(IpdPermissions.view)],
    loadComponent: () => import('./pages/discharge-summary-print.page').then((m) => m.DischargeSummaryPrintPage),
  },
  {
    path: 'setup',
    title: 'Ward & Bed Setup · HMIS',
    canActivate: [permissionGuard(IpdPermissions.view)],
    loadComponent: () => import('./pages/setup.page').then((m) => m.IpdSetupPage),
  },
] satisfies Routes;
