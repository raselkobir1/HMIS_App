import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { filter, switchMap } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { fromIsoDate, toIsoDate, todayIso, hospitalToday } from '../../../shared/utilities/dates';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { Doctor, DoctorApiService } from '../../hr/doctors.models';
import { AppointmentApiService, AppointmentPermissions, DayOfWeek, formatTime, Leave, Schedule, WEEK_DAYS } from '../appointment.models';

/** Doctor sessions (weekly timetable) and leave. Route: /appointments/sessions. */
@Component({
  selector: 'app-schedules-page',
  imports: [FormsModule, MatTableModule, MatFormFieldModule, MatSelectModule, MatButtonModule, MatIconModule, PageHeader, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Doctor sessions" subtitle="Weekly chambers, capacity and leave">
      @if (doctorId()) {
        <button mat-stroked-button type="button" *hasPermission="permissions.schedulesManage" (click)="addLeave()">
          <mat-icon>event_busy</mat-icon> Add leave
        </button>
        <button mat-flat-button type="button" *hasPermission="permissions.schedulesManage" (click)="openSchedule(null)">
          <mat-icon>add</mat-icon> New session
        </button>
      }
    </app-page-header>
    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Doctor</mat-label>
        <mat-select [ngModel]="doctorId()" (ngModelChange)="doctorId.set($event)">
          <mat-option value="">All doctors</mat-option>
          @for (d of doctors(); track d.id) {
            <mat-option [value]="d.employeeId">{{ d.fullName }} · {{ d.specialization }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </section>

    <h3>Sessions</h3>
    <div class="table-card">
      <div class="table-scroll">
        <table mat-table [dataSource]="sortedSchedules()">
          <ng-container matColumnDef="doctor">
            <th mat-header-cell *matHeaderCellDef>Doctor</th>
            <td mat-cell *matCellDef="let s">
              <div>{{ s.doctorName }}</div>
              <div class="secondary">{{ s.specialization }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="day">
            <th mat-header-cell *matHeaderCellDef>Day &amp; time</th>
            <td mat-cell *matCellDef="let s">
              <div>{{ s.dayOfWeek }}</div>
              <div class="secondary">{{ formatTime(s.startTime) }} – {{ formatTime(s.endTime) }}{{ s.room ? ' · ' + s.room : '' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="capacity">
            <th mat-header-cell *matHeaderCellDef>Capacity</th>
            <td mat-cell *matCellDef="let s">{{ s.maxPatients }} patients · {{ s.averageMinutesPerPatient }} min each</td>
          </ng-container>
          <ng-container matColumnDef="effective">
            <th mat-header-cell *matHeaderCellDef>Effective</th>
            <td mat-cell *matCellDef="let s">{{ s.effectiveFrom }} → {{ s.effectiveTo ?? 'ongoing' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let s">
              <span class="badge" [class.ok]="s.isActive" [class.muted]="!s.isActive">{{ s.isActive ? 'Active' : 'Inactive' }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="actions-col wide-actions"></th>
            <td mat-cell *matCellDef="let s" class="actions-col wide-actions">
              <ng-container *hasPermission="permissions.schedulesManage">
                <button mat-icon-button type="button" (click)="openSchedule(s)" [attr.aria-label]="'Edit ' + s.dayOfWeek + ' session'">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button type="button" (click)="toggle(s)" [attr.aria-label]="(s.isActive ? 'Deactivate ' : 'Activate ') + s.dayOfWeek + ' session'">
                  <mat-icon>{{ s.isActive ? 'toggle_on' : 'toggle_off' }}</mat-icon>
                </button>
              </ng-container>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="scheduleColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: scheduleColumns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="scheduleColumns.length">No sessions{{ doctorId() ? ' for this doctor' : '' }}.</td>
          </tr>
        </table>
      </div>
    </div>

    <h3>Upcoming leave</h3>
    <div class="table-card">
      <div class="table-scroll">
        <table mat-table [dataSource]="leaves()">
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let l">{{ l.date }}</td>
          </ng-container>
          <ng-container matColumnDef="doctor">
            <th mat-header-cell *matHeaderCellDef>Doctor</th>
            <td mat-cell *matCellDef="let l">{{ l.doctorName }}</td>
          </ng-container>
          <ng-container matColumnDef="session">
            <th mat-header-cell *matHeaderCellDef>Session</th>
            <td mat-cell *matCellDef="let l">{{ l.sessionLabel ?? 'Whole day' }}</td>
          </ng-container>
          <ng-container matColumnDef="reason">
            <th mat-header-cell *matHeaderCellDef>Reason</th>
            <td mat-cell *matCellDef="let l">{{ l.reason }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="actions-col"></th>
            <td mat-cell *matCellDef="let l" class="actions-col">
              <button mat-icon-button type="button" *hasPermission="permissions.schedulesManage" (click)="removeLeave(l)" [attr.aria-label]="'Remove leave on ' + l.date">
                <mat-icon>delete</mat-icon>
              </button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="leaveColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: leaveColumns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="leaveColumns.length">No upcoming leave.</td>
          </tr>
        </table>
      </div>
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    h3 { margin: 24px 0 8px; font: var(--mat-sys-title-medium); }
    .wide-actions { width: 112px; }
  `,
})
export class SchedulesPage {
  private readonly api = inject(AppointmentApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly permissions = AppointmentPermissions;
  protected readonly formatTime = formatTime;
  protected readonly scheduleColumns = ['doctor', 'day', 'capacity', 'effective', 'status', 'actions'];
  protected readonly leaveColumns = ['date', 'doctor', 'session', 'reason', 'actions'];
  protected readonly doctors = signal<Doctor[]>([]);
  protected readonly doctorId = signal('');
  protected readonly schedules = signal<Schedule[]>([]);
  protected readonly leaves = signal<Leave[]>([]);
  private readonly reload = signal(0);

  protected readonly sortedSchedules = computed(() =>
    [...this.schedules()].sort(
      (a, b) =>
        (a.doctorName ?? '').localeCompare(b.doctorName ?? '') ||
        WEEK_DAYS.indexOf(a.dayOfWeek) - WEEK_DAYS.indexOf(b.dayOfWeek) ||
        a.startTime.localeCompare(b.startTime),
    ),
  );

  constructor() {
    inject(DoctorApiService)
      .search({ pageNumber: 1, pageSize: 100 })
      .subscribe({ next: (page) => this.doctors.set(page.items), error: () => undefined });

    effect(() => {
      const doctorId = this.doctorId() || undefined;
      this.reload();
      this.api.schedules(doctorId).subscribe({ next: (s) => this.schedules.set(s), error: () => this.schedules.set([]) });
      this.api.leaves(doctorId, todayIso()).subscribe({ next: (l) => this.leaves.set(l), error: () => this.leaves.set([]) });
    });
  }

  protected openSchedule(schedule: Schedule | null): void {
    this.dialog
      .open(ScheduleFormDialog, { data: { schedule, doctorId: schedule?.doctorId ?? this.doctorId() }, width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.reload.update((n) => n + 1));
  }

  protected toggle(schedule: Schedule): void {
    const activate = !schedule.isActive;
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: activate ? 'Activate session?' : 'Deactivate session?',
          message: activate
            ? `${schedule.dayOfWeek} ${formatTime(schedule.startTime)} will be open for booking again.`
            : `${schedule.dayOfWeek} ${formatTime(schedule.startTime)} will no longer be offered. Existing bookings are kept.`,
          confirmText: activate ? 'Activate' : 'Deactivate',
          destructive: !activate,
        },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.setScheduleStatus(schedule.id, activate)),
      )
      .subscribe({
        next: () => {
          this.notifications.success(activate ? 'Session activated.' : 'Session deactivated.');
          this.reload.update((n) => n + 1);
        },
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }

  protected addLeave(): void {
    const doctorId = this.doctorId();
    this.dialog
      .open(LeaveFormDialog, {
        data: { doctorId, schedules: this.schedules().filter((s) => s.doctorId === doctorId && s.isActive) },
        width: '480px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.reload.update((n) => n + 1));
  }

  protected removeLeave(leave: Leave): void {
    this.dialog
      .open(ConfirmDialog, {
        data: { title: 'Remove leave?', message: `${leave.doctorName} will be available on ${leave.date} again.`, confirmText: 'Remove', destructive: true },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.removeLeave(leave.id)),
      )
      .subscribe({
        next: () => {
          this.notifications.success('Leave removed.');
          this.reload.update((n) => n + 1);
        },
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }
}

/** "17:00:00" <-> "17:00" for <input type="time">. */
const toTimeInput = (value: string): string => value.slice(0, 5);
const fromTimeInput = (value: string): string => (value.length === 5 ? `${value}:00` : value);

@Component({
  selector: 'app-schedule-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatButtonModule, FieldError],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.schedule ? 'Edit session' : 'New session' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Day</mat-label>
          <mat-select formControlName="dayOfWeek">
            @for (d of days; track d) {
              <mat-option [value]="d">{{ d }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Room / chamber</mat-label>
          <input matInput formControlName="room" maxlength="50" />
          <mat-error><app-field-error [control]="form.controls.room" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Starts</mat-label>
          <input matInput type="time" formControlName="startTime" />
          <mat-error><app-field-error [control]="form.controls.startTime" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Ends</mat-label>
          <input matInput type="time" formControlName="endTime" />
          <mat-error><app-field-error [control]="form.controls.endTime" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Maximum patients</mat-label>
          <input matInput type="number" min="1" max="200" formControlName="maxPatients" />
          <mat-hint>Including walk-ins</mat-hint>
          <mat-error><app-field-error [control]="form.controls.maxPatients" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Minutes per patient</mat-label>
          <input matInput type="number" min="1" max="180" formControlName="averageMinutesPerPatient" />
          <mat-hint>Used for estimated times</mat-hint>
          <mat-error><app-field-error [control]="form.controls.averageMinutesPerPatient" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Effective from</mat-label>
          <input matInput [matDatepicker]="fromPicker" formControlName="effectiveFrom" />
          <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
          <mat-datepicker #fromPicker />
          <mat-error><app-field-error [control]="form.controls.effectiveFrom" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Effective to (optional)</mat-label>
          <input matInput [matDatepicker]="toPicker" formControlName="effectiveTo" />
          <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
          <mat-datepicker #toPicker />
          <mat-error><app-field-error [control]="form.controls.effectiveTo" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; }`,
})
export class ScheduleFormDialog {
  protected readonly data = inject<{ schedule: Schedule | null; doctorId: string }>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ScheduleFormDialog, boolean>);
  private readonly api = inject(AppointmentApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly days = WEEK_DAYS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    dayOfWeek: [this.data.schedule?.dayOfWeek ?? ('Saturday' as DayOfWeek), Validators.required],
    room: [this.data.schedule?.room ?? '', Validators.maxLength(50)],
    startTime: [this.data.schedule ? toTimeInput(this.data.schedule.startTime) : '17:00', Validators.required],
    endTime: [this.data.schedule ? toTimeInput(this.data.schedule.endTime) : '21:00', Validators.required],
    maxPatients: [this.data.schedule?.maxPatients ?? 30, [Validators.required, Validators.min(1), Validators.max(200)]],
    averageMinutesPerPatient: [this.data.schedule?.averageMinutesPerPatient ?? 8, [Validators.required, Validators.min(1), Validators.max(180)]],
    effectiveFrom: [fromIsoDate(this.data.schedule?.effectiveFrom) ?? hospitalToday(), Validators.required],
    effectiveTo: [fromIsoDate(this.data.schedule?.effectiveTo) as Date | null],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const request = {
      dayOfWeek: v.dayOfWeek,
      startTime: fromTimeInput(v.startTime),
      endTime: fromTimeInput(v.endTime),
      maxPatients: Number(v.maxPatients),
      averageMinutesPerPatient: Number(v.averageMinutesPerPatient),
      room: v.room.trim() || null,
      effectiveFrom: toIsoDate(v.effectiveFrom)!,
      effectiveTo: toIsoDate(v.effectiveTo),
    };
    this.saving.set(true);
    this.error.set(null);
    const schedule = this.data.schedule;
    const request$ = schedule
      ? this.api.updateSchedule(schedule.id, { ...request, rowVersion: schedule.rowVersion })
      : this.api.createSchedule({ ...request, doctorId: this.data.doctorId });
    request$.subscribe({
      next: () => {
        this.notifications.success('Session saved.');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}

@Component({
  selector: 'app-leave-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatButtonModule, FieldError],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Add leave</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Date</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="date" [min]="today" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          <mat-error><app-field-error [control]="form.controls.date" /></mat-error>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Session</mat-label>
          <mat-select formControlName="scheduleId">
            <mat-option value="">Whole day</mat-option>
            @for (s of sessionsForDay(); track s.id) {
              <mat-option [value]="s.id">{{ formatTime(s.startTime) }} – {{ formatTime(s.endTime) }}{{ s.room ? ' · ' + s.room : '' }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Reason</mat-label>
          <input matInput formControlName="reason" maxlength="250" />
          <mat-hint>Shown to the front desk when booking</mat-hint>
          <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
        </mat-form-field>
        <p class="note">Leave cannot be recorded while patients are booked for that time — reschedule or cancel them from Appointments first.</p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Add leave</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .full { width: 100%; }
    .note { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class LeaveFormDialog {
  protected readonly data = inject<{ doctorId: string; schedules: Schedule[] }>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<LeaveFormDialog, boolean>);
  private readonly api = inject(AppointmentApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly formatTime = formatTime;
  protected readonly today = hospitalToday();
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    date: [hospitalToday() as Date | null, Validators.required],
    scheduleId: [''],
    reason: ['', [Validators.required, Validators.maxLength(250)]],
  });
  private readonly selectedDate = signal<Date | null>(this.form.controls.date.value);

  /** JS getDay(): 0 = Sunday … 6 = Saturday. */
  private static readonly jsDays: DayOfWeek[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  protected readonly sessionsForDay = computed(() => {
    const date = this.selectedDate();
    return date ? this.data.schedules.filter((s) => s.dayOfWeek === LeaveFormDialog.jsDays[date.getDay()]) : [];
  });

  constructor() {
    this.form.controls.date.valueChanges.subscribe((d) => {
      this.selectedDate.set(d);
      this.form.controls.scheduleId.setValue('');
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api.addLeave({ doctorId: this.data.doctorId, date: toIsoDate(v.date)!, scheduleId: v.scheduleId || null, reason: v.reason.trim() }).subscribe({
      next: () => {
        this.notifications.success('Leave added.');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}
