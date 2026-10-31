import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { AppointmentPermissions } from './appointment.models';

export default [
  {
    path: '',
    pathMatch: 'full',
    title: 'Appointments · HMIS',
    canActivate: [permissionGuard(AppointmentPermissions.view)],
    loadComponent: () => import('./pages/appointments.page').then((m) => m.AppointmentsPage),
  },
  {
    path: 'book',
    title: 'Book Appointment · HMIS',
    canActivate: [permissionGuard(AppointmentPermissions.book)],
    loadComponent: () => import('./pages/book-appointment.page').then((m) => m.BookAppointmentPage),
  },
  {
    path: 'my-queue',
    title: 'My Queue · HMIS',
    canActivate: [permissionGuard(AppointmentPermissions.consult)],
    loadComponent: () => import('./pages/my-queue.page').then((m) => m.MyQueuePage),
  },
  {
    path: 'sessions',
    title: 'Doctor Sessions · HMIS',
    canActivate: [permissionGuard(AppointmentPermissions.view)],
    loadComponent: () => import('./pages/schedules.page').then((m) => m.SchedulesPage),
  },
] satisfies Routes;
