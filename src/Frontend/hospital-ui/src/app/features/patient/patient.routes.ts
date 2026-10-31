import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PatientPermissions } from './patient.models';

export default [
  {
    path: '',
    title: 'Patients · HMIS',
    canActivate: [permissionGuard(PatientPermissions.view)],
    loadComponent: () => import('./pages/patient-search.page').then((m) => m.PatientSearchPage),
  },
  {
    path: 'new',
    title: 'Register Patient · HMIS',
    canActivate: [permissionGuard(PatientPermissions.create)],
    loadComponent: () => import('./pages/patient-form.page').then((m) => m.PatientFormPage),
  },
  {
    path: 'merge-requests',
    title: 'Merge Requests · HMIS',
    canActivate: [permissionGuard(PatientPermissions.mergeApprove)],
    loadComponent: () => import('./pages/merge-requests.page').then((m) => m.MergeRequestsPage),
  },
  {
    path: ':id',
    title: 'Patient · HMIS',
    canActivate: [permissionGuard(PatientPermissions.view)],
    loadComponent: () => import('./pages/patient-profile.page').then((m) => m.PatientProfilePage),
  },
  {
    path: ':id/edit',
    title: 'Edit Patient · HMIS',
    canActivate: [permissionGuard(PatientPermissions.update)],
    loadComponent: () => import('./pages/patient-form.page').then((m) => m.PatientFormPage),
  },
  {
    path: ':id/id-card',
    title: 'Patient ID Card · HMIS',
    canActivate: [permissionGuard(PatientPermissions.view)],
    loadComponent: () => import('./pages/patient-id-card.page').then((m) => m.PatientIdCardPage),
  },
] satisfies Routes;
