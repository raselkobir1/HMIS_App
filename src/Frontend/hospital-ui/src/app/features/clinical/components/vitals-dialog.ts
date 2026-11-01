import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { ClinicalApiService, VitalSign } from '../clinical.models';

export interface VitalsDialogData {
  encounterId: string;
  patientLabel: string;
  /** The encounter id is an IPD admission (ward vitals). */
  inpatient?: boolean;
}

const num = (min: number, max: number) => new FormControl<number | null>(null, [Validators.min(min), Validators.max(max)]);

/** Records one set of vitals for an OPD visit or an inpatient stay. Ranges mirror the server's plausibility checks. */
@Component({
  selector: 'app-vitals-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Record vitals</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">{{ data.patientLabel }}</p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Systolic BP (mmHg)</mat-label>
          <input matInput type="number" formControlName="systolic" />
          <mat-error><app-field-error [control]="form.controls.systolic" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Diastolic BP (mmHg)</mat-label>
          <input matInput type="number" formControlName="diastolic" />
          <mat-error><app-field-error [control]="form.controls.diastolic" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Pulse (/min)</mat-label>
          <input matInput type="number" formControlName="pulse" />
          <mat-error><app-field-error [control]="form.controls.pulse" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Temperature (°F)</mat-label>
          <input matInput type="number" step="0.1" formControlName="temperatureF" />
          <mat-error><app-field-error [control]="form.controls.temperatureF" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Respiratory rate (/min)</mat-label>
          <input matInput type="number" formControlName="respiratoryRate" />
          <mat-error><app-field-error [control]="form.controls.respiratoryRate" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>SpO₂ (%)</mat-label>
          <input matInput type="number" formControlName="spO2" />
          <mat-error><app-field-error [control]="form.controls.spO2" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Weight (kg)</mat-label>
          <input matInput type="number" step="0.1" formControlName="weightKg" />
          <mat-error><app-field-error [control]="form.controls.weightKg" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Height (cm)</mat-label>
          <input matInput type="number" step="0.1" formControlName="heightCm" />
          <mat-hint>{{ bmi() ? 'BMI ' + bmi() : '' }}</mat-hint>
          <mat-error><app-field-error [control]="form.controls.heightCm" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Notes (optional)</mat-label>
          <input matInput formControlName="notes" maxlength="500" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save vitals</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class VitalsDialog {
  protected readonly data = inject<VitalsDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<VitalsDialog, VitalSign>);
  private readonly api = inject(ClinicalApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    systolic: num(40, 300),
    diastolic: num(20, 200),
    pulse: num(20, 250),
    temperatureF: num(85, 115),
    respiratoryRate: num(4, 80),
    spO2: num(50, 100),
    weightKg: num(0.3, 400),
    heightCm: num(30, 250),
    notes: new FormControl<string | null>(null, Validators.maxLength(500)),
  });

  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  protected readonly bmi = computed(() => {
    const { weightKg: w, heightCm: h } = this.values();
    return w && h ? Math.round((w / (h / 100) ** 2) * 10) / 10 : null;
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    const request = { ...v, notes: v.notes?.trim() || null };
    const call = this.data.inpatient
      ? this.api.recordInpatientVitals(this.data.encounterId, request)
      : this.api.recordVitals(this.data.encounterId, request);
    call.subscribe({
      next: (vital) => {
        this.notifications.success('Vitals recorded.');
        this.dialogRef.close(vital);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}
