import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { catchError, filter, of, switchMap, tap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { PagedResponse } from '../../../core/models/api-response.model';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { fromIsoDate, toIsoDate, todayIso, hospitalToday } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { Doctor, DoctorApiService } from '../../hr/doctors.models';
import {
  Appointment,
  AppointmentApiService,
  AppointmentPermissions,
  AppointmentQuery,
  AppointmentStatus,
  formatTime,
  SessionAvailability,
  STATUS_LABELS,
} from '../appointment.models';

type SimpleAction = 'confirm' | 'check-in' | 'no-show';

/** Front-desk appointment list. Route: /appointments (optional ?date=&doctorId=). */
@Component({
  selector: 'app-appointments-page',
  imports: [
    CurrencyPipe,
    FormsModule,
    RouterLink,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Appointments" subtitle="Bookings, check-in and cancellations">
      <a mat-flat-button routerLink="/appointments/book" *hasPermission="permissions.book"><mat-icon>add</mat-icon> Book appointment</a>
    </app-page-header>
    <section class="filters">
      <mat-form-field>
        <mat-label>Date</mat-label>
        <input matInput [matDatepicker]="picker" [ngModel]="dateValue()" (ngModelChange)="onDate($event)" />
        <mat-datepicker-toggle matIconSuffix [for]="picker" />
        <mat-datepicker #picker />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Doctor</mat-label>
        <mat-select [ngModel]="query().doctorId ?? ''" (ngModelChange)="patch({ doctorId: $event || undefined })">
          <mat-option value="">All doctors</mat-option>
          @for (d of doctors(); track d.id) {
            <mat-option [value]="d.employeeId">{{ d.fullName }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Status</mat-label>
        <mat-select [ngModel]="query().status ?? ''" (ngModelChange)="patch({ status: $event || undefined })">
          <mat-option value="">All statuses</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ labels[s] }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </section>
    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="table-scroll">
        <table mat-table [dataSource]="page()?.items ?? []">
          <ng-container matColumnDef="serial">
            <th mat-header-cell *matHeaderCellDef>Serial</th>
            <td mat-cell *matCellDef="let a">
              <div class="serial">{{ a.serialNumber }}</div>
              <div class="secondary">{{ formatTime(a.estimatedTime) }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="patient">
            <th mat-header-cell *matHeaderCellDef>Patient</th>
            <td mat-cell *matCellDef="let a">
              <a [routerLink]="['/patients', a.patientId]">{{ a.patientName ?? 'Unknown' }}</a>
              <div class="secondary">{{ a.patientMRNumber }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="doctor">
            <th mat-header-cell *matHeaderCellDef>Doctor</th>
            <td mat-cell *matCellDef="let a">
              <div>{{ a.doctorName }}</div>
              <div class="secondary">{{ a.appointmentDate }}{{ a.room ? ' · ' + a.room : '' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="visit">
            <th mat-header-cell *matHeaderCellDef>Visit</th>
            <td mat-cell *matCellDef="let a">
              <div>{{ a.visitType === 'New' ? 'New' : 'Follow-up' }} · {{ a.fee | currency: 'BDT' : 'symbol-narrow' : '1.0-0' }}</div>
              <div class="secondary">{{ a.appointmentNumber }} · {{ a.source === 'WalkIn' ? 'Walk-in' : a.source }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let a">
              <span class="badge" [class]="badgeClass(a.status)">{{ labels[a.status] }}</span>
              @if (a.cancellationReason) {
                <div class="secondary">{{ a.cancellationReason }}</div>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="actions-col"></th>
            <td mat-cell *matCellDef="let a" class="actions-col">
              @if (hasActions(a)) {
                <button mat-icon-button type="button" [matMenuTriggerFor]="menu" [attr.aria-label]="'Actions for serial ' + a.serialNumber">
                  <mat-icon>more_vert</mat-icon>
                </button>
                <mat-menu #menu="matMenu">
                  @if (can(a, 'check-in')) {
                    <button mat-menu-item type="button" (click)="run(a, 'check-in')"><mat-icon>how_to_reg</mat-icon> Check in</button>
                  }
                  @if (can(a, 'confirm')) {
                    <button mat-menu-item type="button" (click)="run(a, 'confirm')"><mat-icon>event_available</mat-icon> Confirm</button>
                  }
                  @if (can(a, 'reschedule')) {
                    <button mat-menu-item type="button" (click)="reschedule(a)"><mat-icon>event_repeat</mat-icon> Reschedule</button>
                  }
                  @if (can(a, 'no-show')) {
                    <button mat-menu-item type="button" (click)="run(a, 'no-show')"><mat-icon>person_off</mat-icon> Mark no-show</button>
                  }
                  @if (can(a, 'cancel')) {
                    <button mat-menu-item type="button" (click)="cancel(a)"><mat-icon>event_busy</mat-icon> Cancel</button>
                  }
                </mat-menu>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No appointments for these filters.' }}</td>
          </tr>
        </table>
      </div>
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="query().pageNumber - 1" [pageSize]="query().pageSize"
                     [pageSizeOptions]="[25, 50, 100]" (page)="onPage($event)" aria-label="Appointment pages" />
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `.serial { font: var(--mat-sys-title-medium); }`,
})
export class AppointmentsPage implements OnInit {
  readonly date = input<string>();
  readonly doctorId = input<string>();

  private readonly api = inject(AppointmentApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly permissions = AppointmentPermissions;
  protected readonly formatTime = formatTime;
  protected readonly labels: Record<string, string> = STATUS_LABELS;
  protected readonly statuses = Object.keys(STATUS_LABELS) as AppointmentStatus[];
  protected readonly columns = ['serial', 'patient', 'doctor', 'visit', 'status', 'actions'];
  protected readonly doctors = signal<Doctor[]>([]);
  protected readonly query = signal<AppointmentQuery>({ pageNumber: 1, pageSize: 50, date: todayIso() });
  protected readonly dateValue = signal<Date | null>(hospitalToday());
  protected readonly page = signal<PagedResponse<Appointment> | null>(null);
  protected readonly loading = signal(false);

  constructor() {
    inject(DoctorApiService).bookable().subscribe({ next: (d) => this.doctors.set(d), error: () => undefined });
    toObservable(this.query)
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap((q) => this.api.search(q).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.page.set(page);
        this.loading.set(false);
      });
  }

  ngOnInit(): void {
    const date = this.date();
    if (date) {
      this.dateValue.set(fromIsoDate(date));
    }
    this.patch({ date: date ?? this.query().date, doctorId: this.doctorId() || undefined });
  }

  protected patch(change: Partial<AppointmentQuery>): void {
    this.query.update((q) => ({ ...q, ...change, pageNumber: 1 }));
  }

  protected onDate(value: Date | null): void {
    this.dateValue.set(value);
    this.patch({ date: toIsoDate(value) ?? undefined });
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected badgeClass(status: AppointmentStatus): string {
    switch (status) {
      case 'Waiting':
      case 'InConsultation':
        return 'badge info';
      case 'Completed':
      case 'Confirmed':
        return 'badge ok';
      case 'Cancelled':
      case 'NoShow':
        return 'badge warn';
      default:
        return 'badge muted';
    }
  }

  /** Mirrors the server's status rules so only possible actions are offered. */
  protected can(a: Appointment, action: SimpleAction | 'reschedule' | 'cancel'): boolean {
    const today = todayIso();
    const open = a.status === 'Scheduled' || a.status === 'Confirmed';
    switch (action) {
      case 'confirm':
        return a.status === 'Scheduled' && this.auth.hasPermission(this.permissions.book);
      case 'check-in':
        return open && a.appointmentDate === today && this.auth.hasPermission(this.permissions.checkIn);
      case 'no-show':
        return open && a.appointmentDate <= today && this.auth.hasPermission(this.permissions.checkIn);
      case 'reschedule':
        return open && this.auth.hasPermission(this.permissions.book);
      case 'cancel':
        return (open || a.status === 'Waiting') && this.auth.hasPermission(this.permissions.cancel);
    }
  }

  protected hasActions(a: Appointment): boolean {
    return (['confirm', 'check-in', 'no-show', 'reschedule', 'cancel'] as const).some((x) => this.can(a, x));
  }

  protected run(a: Appointment, action: SimpleAction): void {
    const proceed$ =
      action === 'no-show'
        ? this.dialog
            .open(ConfirmDialog, {
              data: { title: 'Mark as no-show?', message: `Serial ${a.serialNumber} — ${a.patientName} did not arrive.`, confirmText: 'Mark no-show', destructive: true },
            })
            .afterClosed()
            .pipe(filter(Boolean))
        : of(true);
    proceed$.pipe(switchMap(() => this.api.action(a.id, action))).subscribe({
      next: (updated) => {
        this.notifications.success(`Serial ${updated.serialNumber}: ${STATUS_LABELS[updated.status]}.`);
        this.refresh();
      },
      error: (err) => this.notifications.error(apiErrorMessage(err)),
    });
  }

  protected cancel(a: Appointment): void {
    this.dialog
      .open(CancelAppointmentDialog, { data: a, width: '440px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.refresh());
  }

  protected reschedule(a: Appointment): void {
    this.dialog
      .open(RescheduleDialog, { data: a, width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.refresh());
  }

  private refresh(): void {
    this.query.update((q) => ({ ...q }));
  }
}

@Component({
  selector: 'app-cancel-appointment-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Cancel appointment</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <p>Serial {{ appointment.serialNumber }} · {{ appointment.patientName }} with {{ appointment.doctorName }} on {{ appointment.appointmentDate }}.</p>
        <mat-form-field class="full">
          <mat-label>Reason</mat-label>
          <input matInput formControlName="reason" maxlength="250" />
          <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Keep</button>
        <button mat-flat-button type="submit" class="destructive" [disabled]="saving()">Cancel appointment</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .full { width: 100%; }
    .destructive {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class CancelAppointmentDialog {
  protected readonly appointment = inject<Appointment>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CancelAppointmentDialog, boolean>);
  private readonly api = inject(AppointmentApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    reason: ['', [Validators.required, Validators.maxLength(250)]],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.api.cancel(this.appointment.id, this.form.getRawValue().reason.trim()).subscribe({
      next: () => {
        this.notifications.success('Appointment cancelled.');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }
}

@Component({
  selector: 'app-reschedule-dialog',
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatDatepickerModule, MatSelectModule, MatButtonModule],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Reschedule {{ appointment.appointmentNumber }}</h2>
    <mat-dialog-content class="form-grid">
      @if (error(); as message) {
        <div class="banner span" role="alert">{{ message }}</div>
      }
      <p class="span">{{ appointment.patientName }} with {{ appointment.doctorName }} — currently serial {{ appointment.serialNumber }} on {{ appointment.appointmentDate }}.</p>
      <mat-form-field>
        <mat-label>New date</mat-label>
        <input matInput [matDatepicker]="picker" [ngModel]="date()" (ngModelChange)="$event && date.set($event)" [min]="today" />
        <mat-datepicker-toggle matIconSuffix [for]="picker" />
        <mat-datepicker #picker />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Session</mat-label>
        <mat-select [ngModel]="scheduleId()" (ngModelChange)="scheduleId.set($event)">
          @for (s of sessions(); track s.scheduleId) {
            <mat-option [value]="s.scheduleId" [disabled]="!s.isBookable">
              {{ formatTime(s.startTime) }} – {{ formatTime(s.endTime) }} · {{ s.isBookable ? s.remaining + ' left' : s.onLeave ? 'on leave' : s.hasEnded ? 'ended' : 'full' }}
            </mat-option>
          } @empty {
            <mat-option disabled>No session on this date</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
      <button mat-flat-button type="button" [disabled]="saving() || !scheduleId()" (click)="save()">Reschedule</button>
    </mat-dialog-actions>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; }`,
})
export class RescheduleDialog {
  protected readonly appointment = inject<Appointment>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RescheduleDialog, boolean>);
  private readonly api = inject(AppointmentApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly formatTime = formatTime;
  protected readonly today = hospitalToday();
  protected readonly date = signal<Date>(fromIsoDate(this.appointment.appointmentDate) ?? hospitalToday());
  protected readonly sessions = signal<SessionAvailability[]>([]);
  protected readonly scheduleId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const date = toIsoDate(this.date())!;
      this.scheduleId.set(null);
      this.api.availability(this.appointment.doctorId, date).subscribe({
        next: (s) => {
          this.sessions.set(s);
          this.scheduleId.set(s.find((x) => x.isBookable)?.scheduleId ?? null);
        },
        error: () => this.sessions.set([]),
      });
    });
  }

  protected save(): void {
    const scheduleId = this.scheduleId();
    if (!scheduleId) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api.reschedule(this.appointment.id, scheduleId, toIsoDate(this.date())!, this.appointment.rowVersion).subscribe({
      next: (a) => {
        this.notifications.success(`Rescheduled to ${a.appointmentDate}, serial ${a.serialNumber} (about ${formatTime(a.estimatedTime)}).`);
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }
}
