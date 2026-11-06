import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NotificationService } from '../../../core/services/notification.service';
import { fromIsoDate, toIsoDate, hospitalToday } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { HrApiService } from '../hr-api.service';
import { EMPLOYMENT_STATUSES, EmployeeListItem, EmploymentStatus } from '../hr.models';

/** Changes employment status. Leaving (resigned/terminated) requires an exit date. */
@Component({
  selector: 'app-employee-status-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatButtonModule],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Employment status</h2>
    <mat-dialog-content>
      <p>{{ employee.employeeCode }} · {{ employee.fullName }}</p>
      @if (error(); as message) {
        <div class="banner" role="alert">{{ message }}</div>
      }
      <form [formGroup]="form" novalidate>
        <mat-form-field class="full">
          <mat-label>Status</mat-label>
          <mat-select formControlName="status">
            @for (s of statuses; track s.value) {
              <mat-option [value]="s.value">{{ s.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (leaving()) {
          <mat-form-field class="full">
            <mat-label>Exit date</mat-label>
            <input matInput [matDatepicker]="picker" formControlName="exitDate" [min]="joiningDate" required />
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-datepicker #picker />
            <mat-hint>Last working day</mat-hint>
          </mat-form-field>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button type="button" [disabled]="saving()" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
})
export class EmployeeStatusDialog {
  protected readonly employee = inject<EmployeeListItem>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<EmployeeStatusDialog, boolean>);
  private readonly api = inject(HrApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly statuses = EMPLOYMENT_STATUSES;
  protected readonly joiningDate = fromIsoDate(this.employee.joiningDate);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    status: [this.employee.status as EmploymentStatus],
    exitDate: [hospitalToday() as Date | null],
  });

  private readonly status = toSignal(this.form.controls.status.valueChanges, { initialValue: this.employee.status });
  protected leaving(): boolean {
    const status = this.status();
    return status === 'Resigned' || status === 'Terminated';
  }

  protected save(): void {
    const { status, exitDate } = this.form.getRawValue();
    if (this.leaving() && !exitDate) {
      this.error.set('An exit date is required when an employee leaves.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.api.changeStatus(this.employee.id, status, this.leaving() ? toIsoDate(exitDate) : null).subscribe({
      next: () => {
        this.notifications.success('Employment status updated.');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }
}
