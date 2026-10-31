import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { map } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { Doctor, DoctorApiService } from '../../hr/doctors.models';
import { Admission, DISCHARGE_TYPE_LABELS, DischargeType, groupBeds, IpdApiService } from '../ipd.models';

/** Moves the patient to another free bed; the old bed is marked for cleaning. */
@Component({
  selector: 'app-transfer-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Transfer bed</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">{{ admission.summary.patientName }} · now in {{ admission.summary.wardName }} bed {{ admission.summary.bedNumber }}</p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field class="span">
          <mat-label>New bed</mat-label>
          <mat-select formControlName="bedId">
            @for (g of groups(); track g.ward) {
              <mat-optgroup [label]="g.ward">
                @for (b of g.beds; track b.id) {
                  <mat-option [value]="b.id">{{ b.bedNumber }} · {{ b.categoryName }}</mat-option>
                }
              </mat-optgroup>
            } @empty {
              <mat-option disabled>No free beds</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.bedId" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Reason</mat-label>
          <input matInput formControlName="reason" maxlength="200" />
          <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Transfer</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; } .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }`,
})
export class TransferDialog {
  protected readonly admission = inject<Admission>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<TransferDialog, Admission>);
  private readonly api = inject(IpdApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly groups = toSignal(this.api.beds({ assignableOnly: true }).pipe(map(groupBeds)), { initialValue: [] });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    bedId: new FormControl('', { nonNullable: true, validators: Validators.required }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api.transfer(this.admission.summary.id, v.bedId, v.reason.trim(), this.admission.rowVersion).subscribe({
      next: (a) => {
        this.notifications.success(`Transferred to ${a.summary.wardName} bed ${a.summary.bedNumber}.`);
        this.dialogRef.close(a);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}

/** Hands the patient over to another attending doctor. */
@Component({
  selector: 'app-doctor-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Change attending doctor</h2>
    <mat-dialog-content>
      @if (error(); as message) {
        <div class="banner" role="alert">{{ message }}</div>
      }
      <mat-form-field class="full">
        <mat-label>Attending doctor</mat-label>
        <mat-select [formControl]="doctorId">
          @for (d of doctors(); track d.employeeId) {
            <mat-option [value]="d.employeeId">{{ d.fullName }} — {{ d.specialization }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button type="button" [disabled]="saving() || !doctorId.value || doctorId.value === admission.summary.doctorId" (click)="save()">
        Change doctor
      </button>
    </mat-dialog-actions>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.full { min-width: min(420px, 80vw); }`,
})
export class DoctorDialog {
  protected readonly admission = inject<Admission>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DoctorDialog, Admission>);
  private readonly api = inject(IpdApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly doctors = toSignal(
    inject(DoctorApiService)
      .search({ pageNumber: 1, pageSize: 100 })
      .pipe(map((page) => page.items.filter((d) => d.employmentStatus === 'Active' || d.employmentStatus === 'OnLeave'))),
    { initialValue: [] as Doctor[] },
  );
  protected readonly doctorId = new FormControl(this.admission.summary.doctorId, { nonNullable: true });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected save(): void {
    this.saving.set(true);
    this.api.changeDoctor(this.admission.summary.id, this.doctorId.value, this.admission.rowVersion).subscribe({
      next: (a) => {
        this.notifications.success(`Attending doctor: ${a.summary.doctorName}.`);
        this.dialogRef.close(a);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }
}

/** The doctor's discharge order and summary; can be amended until the patient leaves. Never blocked by billing. */
@Component({
  selector: 'app-discharge-order-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatButtonModule, FieldError],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ admission.discharge ? 'Update discharge summary' : 'Order discharge' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">{{ admission.summary.patientName }} · {{ admission.summary.admissionNumber }}</p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Discharge type</mat-label>
          <mat-select formControlName="type">
            @for (t of types; track t) {
              <mat-option [value]="t">{{ typeLabels[t] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Follow-up date (optional)</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="followUpDate" [min]="tomorrow" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          <mat-error><app-field-error [control]="form.controls.followUpDate" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Final diagnosis</mat-label>
          <input matInput formControlName="finalDiagnosis" maxlength="500" />
          <mat-error><app-field-error [control]="form.controls.finalDiagnosis" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Hospital course and treatment</mat-label>
          <textarea matInput formControlName="hospitalCourse" rows="3" maxlength="4000"></textarea>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Medicines at discharge</mat-label>
          <textarea matInput formControlName="dischargeMedications" rows="3" maxlength="2000"></textarea>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Advice (English or বাংলা)</mat-label>
          <textarea matInput formControlName="advice" rows="2" maxlength="2000" lang="bn"></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ admission.discharge ? 'Save summary' : 'Order discharge' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; } .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }`,
})
export class DischargeOrderDialog {
  protected readonly admission = inject<Admission>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DischargeOrderDialog, Admission>);
  private readonly api = inject(IpdApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly types = Object.keys(DISCHARGE_TYPE_LABELS) as DischargeType[];
  protected readonly typeLabels = DISCHARGE_TYPE_LABELS;
  protected readonly tomorrow = new Date(Date.now() + 86_400_000);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly existing = this.admission.discharge;
  protected readonly form = new FormGroup({
    type: new FormControl<DischargeType>(this.existing?.type ?? 'Normal', { nonNullable: true }),
    finalDiagnosis: new FormControl(this.existing?.finalDiagnosis ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }),
    hospitalCourse: new FormControl<string | null>(this.existing?.hospitalCourse ?? null),
    dischargeMedications: new FormControl<string | null>(this.existing?.dischargeMedications ?? null),
    advice: new FormControl<string | null>(this.existing?.advice ?? null),
    followUpDate: new FormControl<Date | null>(fromIsoDate(this.existing?.followUpDate)),
  });
  private readonly type = toSignal(this.form.controls.type.valueChanges, { initialValue: this.form.controls.type.value });
  protected readonly isDeath = computed(() => this.type() === 'Death');

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const clean = (value: string | null) => value?.trim() || null;
    this.saving.set(true);
    this.error.set(null);
    this.api
      .orderDischarge(this.admission.summary.id, {
        type: v.type,
        finalDiagnosis: v.finalDiagnosis.trim(),
        hospitalCourse: clean(v.hospitalCourse),
        dischargeMedications: clean(v.dischargeMedications),
        advice: clean(v.advice),
        followUpDate: this.isDeath() ? null : toIsoDate(v.followUpDate),
        rowVersion: this.admission.rowVersion,
      })
      .subscribe({
        next: (a) => {
          this.notifications.success(this.existing ? 'Discharge summary saved.' : 'Discharge ordered. The front desk can now prepare the final bill.');
          this.dialogRef.close(a);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}
