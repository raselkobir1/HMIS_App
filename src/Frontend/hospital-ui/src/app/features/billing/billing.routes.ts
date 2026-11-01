import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { BillingPermissions } from './billing.models';

export default [
  { path: '', pathMatch: 'full', redirectTo: 'invoices' },
  {
    path: 'settings',
    title: 'Billing Settings · HMIS',
    canActivate: [permissionGuard(BillingPermissions.settingsManage)],
    loadComponent: () => import('./pages/billing-settings.page').then((m) => m.BillingSettingsPage),
  },
  {
    path: 'invoices',
    title: 'Invoices · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/invoices.page').then((m) => m.InvoicesPage),
  },
  {
    path: 'invoices/new',
    title: 'New Invoice · HMIS',
    canActivate: [permissionGuard(BillingPermissions.invoiceManage)],
    loadComponent: () => import('./pages/invoice-editor.page').then((m) => m.InvoiceEditorPage),
  },
  {
    path: 'invoices/:id',
    title: 'Invoice · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/invoice.page').then((m) => m.InvoicePage),
  },
  {
    path: 'invoices/:id/edit',
    title: 'Edit Invoice · HMIS',
    canActivate: [permissionGuard(BillingPermissions.invoiceManage)],
    loadComponent: () => import('./pages/invoice-editor.page').then((m) => m.InvoiceEditorPage),
  },
  {
    path: 'invoices/:id/print',
    title: 'Invoice · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/bill-print.pages').then((m) => m.InvoicePrintPage),
  },
  {
    path: 'receipts/:id/print',
    title: 'Receipt · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/bill-print.pages').then((m) => m.ReceiptPrintPage),
  },
  {
    path: 'patients/:patientId',
    title: 'Patient Account · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/patient-account.page').then((m) => m.PatientAccountPage),
  },
  {
    path: 'approvals',
    title: 'Billing Approvals · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/approvals.page').then((m) => m.ApprovalsPage),
  },
  {
    path: 'collections',
    title: 'Collections · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/collections.page').then((m) => m.CollectionsPage),
  },
  {
    path: 'services',
    title: 'Price List · HMIS',
    canActivate: [permissionGuard(BillingPermissions.view)],
    loadComponent: () => import('./pages/services.page').then((m) => m.ServicesPage),
  },
] satisfies Routes;
