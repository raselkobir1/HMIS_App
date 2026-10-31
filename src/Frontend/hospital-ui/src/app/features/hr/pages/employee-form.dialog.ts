import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { applyServerErrors } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../../administration/administration-api.service';
import { HrApiService } from '../hr-api.service';
import { EMPLOYMENT_TYPES, EmployeeDetail, EmploymentType, Gender, GENDERS } from '../hr.models';

interface Option {
  id: string;
  name: string;
}

@Component({
  selector: 'app-employee-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    FieldError,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ employee ? 'Edit ' + employee.employeeCode : 'New employee' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (bannerError(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        @if (!employee) {
          <p class="hint span">The employee code is generated automatically when you save.</p>
        }
        <mat-form-field class="span">
          <mat-label>Full name</mat-label>
          <input matInput formControlName="fullName" />
          <mat-error><app-field-error [control]="form.controls.fullName" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Gender</mat-label>
          <mat-select formControlName="gender">
            @for (g of genders; track g) {
              <mat-option [value]="g">{{ g }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Date of birth</mat-label>
          <input matInput [matDatepicker]="dobPicker" formControlName="dateOfBirth" [max]="today" />
          <mat-datepicker-toggle matIconSuffix [for]="dobPicker" />
          <mat-datepicker #dobPicker startView="multi-year" />
          <mat-error><app-field-error [control]="form.controls.dateOfBirth" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Mobile number</mat-label>
          <input matInput type="tel" formControlName="mobileNumber" />
          <mat-error><app-field-error [control]="form.controls.mobileNumber" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" />
          <mat-error><app-field-error [control]="form.controls.email" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Address</mat-label>
          <textarea matInput formControlName="address" rows="2"></textarea>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Department</mat-label>
          <mat-select formControlName="departmentId">
            @for (d of departmentOptions(); track d.id) {
              <mat-option [value]="d.id">{{ d.name }}</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.departmentId" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Designation</mat-label>
          <mat-select formControlName="designationId">
            @for (d of designationOptions(); track d.id) {
              <mat-option [value]="d.id">{{ d.name }}</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.designationId" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Employment type</mat-label>
          <mat-select formControlName="employmentType">
            @for (t of types; track t) {
              <mat-option [value]="t">{{ t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Joining date</mat-label>
          <input matInput [matDatepicker]="joinPicker" formControlName="joiningDate" />
          <mat-datepicker-toggle matIconSuffix [for]="joinPicker" />
          <mat-datepicker #joinPicker />
          <mat-error><app-field-error [control]="form.controls.joiningDate" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ employee ? 'Save' : 'Register' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span {
      grid-column: 1 / -1;
    }
  `,
})
export class EmployeeFormDialog {
  protected readonly employee = inject<EmployeeDetail | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<EmployeeFormDialog, boolean>);
  private readonly api = inject(HrApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly genders = GENDERS;
  protected readonly types = EMPLOYMENT_TYPES;
  protected readonly today = new Date();
  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  private readonly departments = signal<Option[]>([]);
  private readonly designations = signal<Option[]>([]);

  /** Active options, plus the employee's current one if it has since been deactivated (so it still displays). */
  protected readonly departmentOptions = computed(() =>
    withCurrent(this.departments(), this.employee?.departmentId, this.employee?.departmentName));
  protected readonly designationOptions = computed(() =>
    withCurrent(this.designations(), this.employee?.designationId, this.employee?.designationName));

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: [this.employee?.fullName ?? '', [Validators.required, Validators.maxLength(150)]],
    gender: [this.employee?.gender ?? ('Male' as Gender), Validators.required],
    dateOfBirth: [fromIsoDate(this.employee?.dateOfBirth) as Date | null],
    mobileNumber: [this.employee?.mobileNumber ?? '', [Validators.required, Validators.pattern(/^\+?[0-9\s-]{6,20}$/)]],
    email: [this.employee?.email ?? '', [Validators.email, Validators.maxLength(256)]],
    address: [this.employee?.address ?? '', Validators.maxLength(500)],
    departmentId: [this.employee?.departmentId ?? '', Validators.required],
    designationId: [this.employee?.designationId ?? '', Validators.required],
    employmentType: [this.employee?.employmentType ?? ('Permanent' as EmploymentType), Validators.required],
    joiningDate: [fromIsoDate(this.employee?.joiningDate) ?? (new Date() as Date | null), Validators.required],
  });

  constructor() {
    const admin = inject(AdministrationApiService);
    forkJoin([admin.departmentLookup(), admin.searchDesignations({ pageNumber: 1, pageSize: 100, isActive: true })]).subscribe(
      ([departments, designations]) => {
        this.departments.set(departments.map((d) => ({ id: d.id, name: d.name })));
        this.designations.set(designations.items.map((d) => ({ id: d.id, name: d.name })));
      },
    );
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    const v = this.form.getRawValue();
    const request = {
      fullName: v.fullName,
      gender: v.gender,
      dateOfBirth: toIsoDate(v.dateOfBirth),
      mobileNumber: v.mobileNumber,
      email: v.email.trim() || null,
      address: v.address.trim() || null,
      departmentId: v.departmentId,
      designationId: v.designationId,
      employmentType: v.employmentType,
      joiningDate: toIsoDate(v.joiningDate)!,
    };

    const request$ = this.employee
      ? this.api.update(this.employee.id, { ...request, rowVersion: this.employee.rowVersion })
      : this.api.create(request);

    request$.subscribe({
      next: (saved) => {
        this.notifications.success(this.employee ? 'Employee updated.' : `Employee ${saved.employeeCode} registered.`);
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }
}

function withCurrent(options: Option[], currentId?: string, currentName?: string | null): Option[] {
  return currentId && !options.some((o) => o.id === currentId)
    ? [...options, { id: currentId, name: `${currentName ?? 'Unknown'} (inactive)` }]
    : options;
}
