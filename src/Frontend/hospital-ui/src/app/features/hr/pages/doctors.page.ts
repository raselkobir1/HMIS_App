import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../../core/models/api-response.model';
import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { EmployeePicker } from '../components/employee-picker';
import { Doctor, DoctorApiService, DoctorPermissions, DoctorQuery } from '../doctors.models';

@Component({
  selector: 'app-doctors-page',
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Doctors" subtitle="Doctor profiles and consultation fees">
      <button mat-flat-button type="button" *hasPermission="permissions.manage" (click)="open(null)">
        <mat-icon>add</mat-icon> New doctor profile
      </button>
    </app-page-header>
    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Search</mat-label>
        <input matInput [formControl]="search" placeholder="Name, code or specialization" />
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
    </section>
    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="table-scroll">
        <table mat-table [dataSource]="page()?.items ?? []">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Doctor</th>
            <td mat-cell *matCellDef="let d">
              <div>{{ d.fullName }}</div>
              <div class="secondary">{{ d.qualifications }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="specialization">
            <th mat-header-cell *matHeaderCellDef>Specialization / Department</th>
            <td mat-cell *matCellDef="let d">
              <div>{{ d.specialization }}</div>
              <div class="secondary">{{ d.departmentName ?? '—' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="registration">
            <th mat-header-cell *matHeaderCellDef>BMDC reg.</th>
            <td mat-cell *matCellDef="let d">{{ d.registrationNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="fees">
            <th mat-header-cell *matHeaderCellDef>Fee (new / follow-up)</th>
            <td mat-cell *matCellDef="let d">
              {{ d.newPatientFee | currency: 'BDT' : 'symbol-narrow' : '1.0-0' }} / {{ d.followUpFee | currency: 'BDT' : 'symbol-narrow' : '1.0-0' }}
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Appointments</th>
            <td mat-cell *matCellDef="let d">
              <span class="badge" [class.ok]="d.isAcceptingAppointments" [class.muted]="!d.isAcceptingAppointments">
                {{ d.isAcceptingAppointments ? 'Accepting' : 'Not accepting' }}
              </span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="actions-col"></th>
            <td mat-cell *matCellDef="let d" class="actions-col">
              <button mat-icon-button type="button" *hasPermission="permissions.manage" (click)="open(d)" [attr.aria-label]="'Edit ' + d.fullName">
                <mat-icon>edit</mat-icon>
              </button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No doctor profiles yet.' }}</td>
          </tr>
        </table>
      </div>
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="query().pageNumber - 1" [pageSize]="query().pageSize"
                     [pageSizeOptions]="[20, 50, 100]" (page)="onPage($event)" aria-label="Doctor pages" />
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class DoctorsPage {
  private readonly api = inject(DoctorApiService);
  private readonly dialog = inject(MatDialog);

  protected readonly permissions = DoctorPermissions;
  protected readonly columns = ['name', 'specialization', 'registration', 'fees', 'status', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly query = signal<DoctorQuery>({ pageNumber: 1, pageSize: 20 });
  protected readonly page = signal<PagedResponse<Doctor> | null>(null);
  protected readonly loading = signal(false);

  constructor() {
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
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((s) => this.query.update((q) => ({ ...q, search: s.trim() || undefined, pageNumber: 1 })));
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected open(doctor: Doctor | null): void {
    this.dialog
      .open(DoctorFormDialog, { data: doctor, width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.query.update((q) => ({ ...q })));
  }
}

@Component({
  selector: 'app-doctor-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatCheckboxModule, MatButtonModule, FieldError, EmployeePicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ doctor ? doctor.fullName : 'New doctor profile' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        @if (!doctor) {
          <div class="span">
            <app-employee-picker [(employeeId)]="employeeId" label="Employee" hint="Must have a Doctor-category designation" />
          </div>
        }
        <mat-form-field>
          <mat-label>Specialization</mat-label>
          <input matInput formControlName="specialization" placeholder="e.g. Cardiology" />
          <mat-error><app-field-error [control]="form.controls.specialization" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>BMDC registration no.</mat-label>
          <input matInput formControlName="registrationNumber" />
          <mat-error><app-field-error [control]="form.controls.registrationNumber" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Qualifications</mat-label>
          <input matInput formControlName="qualifications" placeholder="e.g. MBBS, FCPS (Medicine)" />
          <mat-error><app-field-error [control]="form.controls.qualifications" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>New patient fee (৳)</mat-label>
          <input matInput type="number" min="0" formControlName="newPatientFee" />
          <mat-error><app-field-error [control]="form.controls.newPatientFee" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Follow-up fee (৳)</mat-label>
          <input matInput type="number" min="0" formControlName="followUpFee" />
          <mat-error><app-field-error [control]="form.controls.followUpFee" /></mat-error>
        </mat-form-field>
        @if (doctor) {
          <mat-checkbox formControlName="isAcceptingAppointments" class="span">Accepting new appointments</mat-checkbox>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving() || (!doctor && !employeeId())">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; }`,
})
export class DoctorFormDialog {
  protected readonly doctor = inject<Doctor | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DoctorFormDialog, boolean>);
  private readonly api = inject(DoctorApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly employeeId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    specialization: [this.doctor?.specialization ?? '', [Validators.required, Validators.maxLength(100)]],
    qualifications: [this.doctor?.qualifications ?? '', [Validators.required, Validators.maxLength(250)]],
    registrationNumber: [this.doctor?.registrationNumber ?? '', [Validators.required, Validators.maxLength(30)]],
    newPatientFee: [this.doctor?.newPatientFee ?? 0, [Validators.required, Validators.min(0)]],
    followUpFee: [this.doctor?.followUpFee ?? 0, [Validators.required, Validators.min(0)]],
    isAcceptingAppointments: [this.doctor?.isAcceptingAppointments ?? true],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const { isAcceptingAppointments, ...v } = this.form.getRawValue();
    const request$ = this.doctor
      ? this.api.update(this.doctor.id, { ...v, isAcceptingAppointments, rowVersion: this.doctor.rowVersion })
      : this.api.create({ ...v, employeeId: this.employeeId()! });
    request$.subscribe({
      next: () => {
        this.notifications.success('Doctor profile saved.');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}
