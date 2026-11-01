import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { MEDICINE_FORMS, ROUTES } from '../../clinical/clinical.models';
import {
  Allergy,
  FREQUENCIES,
  fromLocalInput,
  MedicationOrder,
  NOTE_KIND_LABELS,
  NursingApiService,
  ProgressNote,
  ProgressNoteKind,
  toLocalInput,
} from '../nursing.models';

export interface ChartDialogData {
  admissionId: string;
  patientLabel: string;
  allergies?: Allergy[];
}

const text = (max: number) => new FormControl<string>('', { nonNullable: true, validators: Validators.maxLength(max) });

/** A signed SOAP note. Notes are never edited; the author can mark one as entered in error and write a new one. */
@Component({
  selector: 'app-progress-note-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Progress note</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">{{ data.patientLabel }}</p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field class="span">
          <mat-label>Type</mat-label>
          <mat-select formControlName="kind">
            @for (k of kinds; track k) {
              <mat-option [value]="k">{{ kindLabels[k] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Subjective (complaints, overnight events)</mat-label>
          <textarea matInput formControlName="subjective" rows="2" maxlength="4000"></textarea>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Objective (examination, results)</mat-label>
          <textarea matInput formControlName="objective" rows="2" maxlength="4000"></textarea>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Assessment</mat-label>
          <textarea matInput formControlName="assessment" rows="2" maxlength="4000"></textarea>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Plan</mat-label>
          <textarea matInput formControlName="plan" rows="2" maxlength="4000"></textarea>
        </mat-form-field>
        <p class="span hint">The note is signed with your name when saved and cannot be edited afterwards.</p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Sign note</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; } .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }`,
})
export class ProgressNoteDialog {
  protected readonly data = inject<ChartDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ProgressNoteDialog, ProgressNote>);
  private readonly api = inject(NursingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly kinds = Object.keys(NOTE_KIND_LABELS) as ProgressNoteKind[];
  protected readonly kindLabels = NOTE_KIND_LABELS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    kind: new FormControl<ProgressNoteKind>('WardRound', { nonNullable: true }),
    subjective: text(4000),
    objective: text(4000),
    assessment: text(4000),
    plan: text(4000),
  });

  protected save(): void {
    const v = this.form.getRawValue();
    const clean = (s: string) => s.trim() || null;
    if (![v.subjective, v.objective, v.assessment, v.plan].some((s) => s.trim())) {
      this.error.set('Write at least one part of the note.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api
      .addProgressNote(this.data.admissionId, {
        kind: v.kind,
        subjective: clean(v.subjective),
        objective: clean(v.objective),
        assessment: clean(v.assessment),
        plan: clean(v.plan),
      })
      .subscribe({
        next: (note) => {
          this.notifications.success('Progress note signed.');
          this.dialogRef.close(note);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err));
        },
      });
  }
}

/** A new inpatient medication order. Orders are never edited: change = stop + new order. */
@Component({
  selector: 'app-medication-order-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Order medication</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">{{ data.patientLabel }}</p>
        @if (data.allergies?.length) {
          <div class="banner span allergy" role="alert">
            <mat-icon>warning</mat-icon>
            Allergies: {{ allergyText }}
          </div>
        }
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field class="span">
          <mat-label>Medicine</mat-label>
          <input matInput formControlName="medicineName" maxlength="150" placeholder="e.g. Inj. Ceftriaxone" />
          <mat-error><app-field-error [control]="form.controls.medicineName" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Form</mat-label>
          <mat-select formControlName="form">
            <mat-option [value]="null">—</mat-option>
            @for (f of forms; track f) {
              <mat-option [value]="f">{{ f }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Strength</mat-label>
          <input matInput formControlName="strength" maxlength="50" placeholder="e.g. 1 g" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Dose</mat-label>
          <input matInput formControlName="dose" maxlength="50" placeholder="e.g. 1 g, 1 tab, 10 ml" />
          <mat-error><app-field-error [control]="form.controls.dose" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Route</mat-label>
          <mat-select formControlName="route">
            @for (r of routes; track r) {
              <mat-option [value]="r">{{ r }}</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.route" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Frequency</mat-label>
          <mat-select formControlName="frequency">
            @for (f of frequencies; track f.value) {
              <mat-option [value]="f.value">{{ f.label }}</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.frequency" /></mat-error>
        </mat-form-field>
        @if (frequency() === 'PRN') {
          <mat-form-field class="span">
            <mat-label>Give when</mat-label>
            <input matInput formControlName="prnIndication" maxlength="200" placeholder="e.g. temperature above 100°F, max 4 doses a day" />
            <mat-error><app-field-error [control]="form.controls.prnIndication" /></mat-error>
          </mat-form-field>
        }
        <mat-form-field>
          <mat-label>Start</mat-label>
          <input matInput type="datetime-local" formControlName="startsAt" />
          <mat-hint>Within the last hour or the next 7 days</mat-hint>
        </mat-form-field>
        @if (frequency() !== 'STAT') {
          <mat-form-field>
            <mat-label>Duration (days)</mat-label>
            <input matInput type="number" formControlName="durationDays" min="1" max="90" />
            <mat-hint>Leave empty to continue until stopped</mat-hint>
            <mat-error><app-field-error [control]="form.controls.durationDays" /></mat-error>
          </mat-form-field>
        }
        <mat-form-field class="span">
          <mat-label>Instructions (optional)</mat-label>
          <input matInput formControlName="instructions" maxlength="300" placeholder="e.g. after skin test; infuse over 30 minutes" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Sign order</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }
    .allergy { display: flex; align-items: center; gap: 8px; font-weight: 500; }
  `,
})
export class MedicationOrderDialog {
  protected readonly data = inject<ChartDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<MedicationOrderDialog, MedicationOrder>);
  private readonly api = inject(NursingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly allergyText = (this.data.allergies ?? []).map((a) => `${a.allergen} (${a.severity.toLowerCase()})`).join(', ');
  protected readonly forms = MEDICINE_FORMS;
  protected readonly routes = ROUTES;
  protected readonly frequencies = FREQUENCIES;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    medicineName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(150)] }),
    form: new FormControl<string | null>(null),
    strength: new FormControl<string | null>(null, Validators.maxLength(50)),
    dose: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] }),
    route: new FormControl('', { nonNullable: true, validators: Validators.required }),
    frequency: new FormControl('', { nonNullable: true, validators: Validators.required }),
    prnIndication: new FormControl<string | null>(null, Validators.maxLength(200)),
    startsAt: new FormControl(toLocalInput(new Date()), { nonNullable: true }),
    durationDays: new FormControl<number | null>(null, [Validators.min(1), Validators.max(90)]),
    instructions: new FormControl<string | null>(null, Validators.maxLength(300)),
  });
  protected readonly frequency = toSignal(this.form.controls.frequency.valueChanges, { initialValue: '' });

  protected save(): void {
    const v = this.form.getRawValue();
    if (v.frequency === 'PRN' && !v.prnIndication?.trim()) {
      this.form.controls.prnIndication.setErrors({ required: true });
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api
      .orderMedication(this.data.admissionId, {
        medicineName: v.medicineName.trim(),
        form: v.form,
        strength: v.strength?.trim() || null,
        dose: v.dose.trim(),
        route: v.route,
        frequency: v.frequency,
        // Left at the default: "now" to the second, set by the server.
        startsAt: this.form.controls.startsAt.dirty ? fromLocalInput(v.startsAt) : null,
        durationDays: v.frequency === 'STAT' ? null : v.durationDays,
        prnIndication: v.frequency === 'PRN' ? v.prnIndication!.trim() : null,
        instructions: v.instructions?.trim() || null,
      })
      .subscribe({
        next: (order) => {
          this.notifications.success(`${order.medicineName} ordered.`);
          this.dialogRef.close(order);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}
