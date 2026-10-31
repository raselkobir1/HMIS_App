import { inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { CanDeactivateFn, Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { ConfirmDialog } from '../../shared/components/confirm-dialog/confirm-dialog';
import { ClinicalPermissions } from './clinical.models';
import type { ConsultationPage } from './pages/consultation.page';

/** Asks before leaving a consultation with unsaved edits. */
const unsavedConsultationGuard: CanDeactivateFn<ConsultationPage> = (page) =>
  !page.hasUnsavedChanges() ||
  inject(MatDialog)
    .open(ConfirmDialog, {
      data: {
        title: 'Leave without saving?',
        message: 'The consultation has unsaved changes. They will be lost if you leave this page.',
        confirmText: 'Leave',
        cancelText: 'Stay',
        destructive: true,
      },
    })
    .afterClosed();

export default [
  { path: '', pathMatch: 'full', redirectTo: 'vitals' },
  {
    path: 'vitals',
    title: 'OPD Vitals · HMIS',
    canActivate: [permissionGuard(ClinicalPermissions.vitalsRecord)],
    loadComponent: () => import('./pages/vitals-station.page').then((m) => m.VitalsStationPage),
  },
  {
    path: 'encounters/:encounterId',
    title: 'Consultation · HMIS',
    canActivate: [permissionGuard(ClinicalPermissions.view)],
    canDeactivate: [unsavedConsultationGuard],
    loadComponent: () => import('./pages/consultation.page').then((m) => m.ConsultationPage),
  },
  {
    path: 'consultations/:id',
    title: 'Consultation · HMIS',
    canActivate: [permissionGuard(ClinicalPermissions.view)],
    loadComponent: () => import('./pages/consultation-view.page').then((m) => m.ConsultationViewPage),
  },
  {
    path: 'consultations/:id/print',
    title: 'Prescription · HMIS',
    canActivate: [permissionGuard(ClinicalPermissions.view)],
    loadComponent: () => import('./pages/prescription-print.page').then((m) => m.PrescriptionPrintPage),
  },
] satisfies Routes;
