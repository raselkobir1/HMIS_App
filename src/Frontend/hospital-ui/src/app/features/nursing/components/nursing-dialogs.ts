import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { catchError, debounceTime, distinctUntilChanged, of, startWith, switchMap } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { EmployeeLookupItem } from '../../hr/hr.models';
import { HrApiService } from '../../hr/hr-api.service';
import {
  Administration,
  AdministrationStatus,
  Allergy,
  DoseSlot,
  FluidDirection,
  FluidEntry,
  fromLocalInput,
  MarOrder,
  NURSING_NOTE_LABELS,
  NursingApiService,
  NursingNote,
  NursingNoteKind,
  RosterEntry,
  Shift,
  SHIFT_LABELS,
  SHIFTS,
  toLocalInput,
  WardOption,
} from '../nursing.models';

export interface AdministerDialogData {
  admissionId: string;
  patientLabel: string;
  allergies: Allergy[];
  order: MarOrder;
  /** The scheduled dose; null for a PRN dose. */
  slot: DoseSlot | null;
}

/** Records one dose on the MAR: given, or omitted / refused / held with a reason. */
@Component({
  selector: 'app-administer-dialog',
  imports: [DatePipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatButtonToggleModule, MatIconModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.slot ? 'Dose due ' + (data.slot.scheduledAt | date: 'h:mm a, d MMM') : 'PRN dose' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <div class="span order">
          <strong>{{ o.medicineName }} {{ o.strength }}</strong> · {{ o.dose }} {{ o.route }} · {{ o.frequency }}
          @if (o.prnIndication) {
            <div class="hint">Give when: {{ o.prnIndication }}</div>
          }
          @if (o.instructions) {
            <div class="hint">{{ o.instructions }}</div>
          }
          <div class="hint">{{ data.patientLabel }}</div>
        </div>
        @if (allergyText) {
          <div class="banner span allergy" role="alert"><mat-icon>warning</mat-icon> Allergies: {{ allergyText }}</div>
        }
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-button-toggle-group class="span" formControlName="status" aria-label="Outcome">
          @for (s of statuses; track s) {
            <mat-button-toggle [value]="s">{{ s }}</mat-button-toggle>
          }
        </mat-button-toggle-group>
        <mat-form-field>
          <mat-label>{{ given() ? 'Given at' : 'Time' }}</mat-label>
          <input matInput type="datetime-local" formControlName="occurredAt" />
        </mat-form-field>
        @if (given()) {
          <mat-form-field>
            <mat-label>Dose given</mat-label>
            <input matInput formControlName="doseGiven" maxlength="50" />
          </mat-form-field>
        } @else {
          <mat-form-field>
            <mat-label>Reason</mat-label>
            <input matInput formControlName="reason" maxlength="250" placeholder="e.g. NPO for surgery, BP low" required />
            <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
          </mat-form-field>
        }
        <mat-form-field class="span">
          <mat-label>Notes (optional)</mat-label>
          <input matInput formControlName="notes" maxlength="500" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Record</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .order { margin-bottom: 8px; }
    .allergy { display: flex; align-items: center; gap: 8px; font-weight: 500; }
    mat-button-toggle-group { margin-bottom: 12px; width: fit-content; }
  `,
})
export class AdministerDialog {
  protected readonly data = inject<AdministerDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<AdministerDialog, Administration>);
  private readonly api = inject(NursingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly o = this.data.order;
  protected readonly allergyText = this.data.allergies.map((a) => `${a.allergen} (${a.severity.toLowerCase()})`).join(', ');
  protected readonly statuses: AdministrationStatus[] = ['Given', 'Omitted', 'Refused', 'Held'];
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    status: new FormControl<AdministrationStatus>('Given', { nonNullable: true }),
    occurredAt: new FormControl(toLocalInput(new Date()), { nonNullable: true, validators: Validators.required }),
    doseGiven: new FormControl(this.data.order.dose, { nonNullable: true, validators: Validators.maxLength(50) }),
    reason: new FormControl('', { nonNullable: true, validators: Validators.maxLength(250) }),
    notes: new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) }),
  });
  private readonly status = toSignal(this.form.controls.status.valueChanges, { initialValue: 'Given' as AdministrationStatus });
  protected readonly given = () => this.status() === 'Given';

  constructor() {
    // A dose that is not given needs a reason.
    this.form.controls.status.valueChanges.pipe(takeUntilDestroyed()).subscribe((status) => {
      const reason = this.form.controls.reason;
      reason.setValidators(status === 'Given' ? Validators.maxLength(250) : [Validators.required, Validators.maxLength(250)]);
      reason.updateValueAndValidity();
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
    this.api
      .recordDose(this.data.admissionId, {
        orderId: this.o.id,
        scheduledAt: this.data.slot?.scheduledAt ?? null,
        status: v.status,
        // Left at the default: "now" to the second, set by the server.
        occurredAt: this.form.controls.occurredAt.dirty ? fromLocalInput(v.occurredAt) : null,
        doseGiven: v.status === 'Given' ? v.doseGiven.trim() || null : null,
        reason: v.status === 'Given' ? null : v.reason.trim(),
        notes: v.notes.trim() || null,
      })
      .subscribe({
        next: (entry) => {
          this.notifications.success(`${this.o.medicineName}: ${entry.status.toLowerCase()} recorded.`);
          this.dialogRef.close(entry);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err));
        },
      });
  }
}

export interface FluidDialogData {
  admissionId: string;
  intakeCategories: string[];
  outputCategories: string[];
}

/** One intake or output measurement in ml. */
@Component({
  selector: 'app-fluid-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatButtonToggleModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Intake / output</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-button-toggle-group class="span" formControlName="direction" aria-label="Intake or output">
          <mat-button-toggle value="Intake">Intake</mat-button-toggle>
          <mat-button-toggle value="Output">Output</mat-button-toggle>
        </mat-button-toggle-group>
        <mat-form-field>
          <mat-label>Type</mat-label>
          <mat-select formControlName="category">
            @for (c of categories(); track c) {
              <mat-option [value]="c">{{ c }}</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.category" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Volume (ml)</mat-label>
          <input matInput type="number" formControlName="volumeMl" min="1" max="5000" />
          <mat-error><app-field-error [control]="form.controls.volumeMl" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Time</mat-label>
          <input matInput type="datetime-local" formControlName="measuredAt" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Notes (optional)</mat-label>
          <input matInput formControlName="notes" maxlength="300" placeholder="e.g. DNS 500 ml" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; } mat-button-toggle-group { margin-bottom: 12px; width: fit-content; }`,
})
export class FluidDialog {
  protected readonly data = inject<FluidDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<FluidDialog, FluidEntry>);
  private readonly api = inject(NursingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    direction: new FormControl<FluidDirection>('Intake', { nonNullable: true }),
    category: new FormControl('', { nonNullable: true, validators: Validators.required }),
    volumeMl: new FormControl<number | null>(null, [Validators.required, Validators.min(1), Validators.max(5000)]),
    measuredAt: new FormControl(toLocalInput(new Date()), { nonNullable: true, validators: Validators.required }),
    notes: new FormControl('', { nonNullable: true, validators: Validators.maxLength(300) }),
  });
  private readonly direction = toSignal(this.form.controls.direction.valueChanges, { initialValue: 'Intake' as FluidDirection });
  protected readonly categories = () => (this.direction() === 'Intake' ? this.data.intakeCategories : this.data.outputCategories);

  constructor() {
    this.form.controls.direction.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.form.controls.category.setValue(''));
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .recordFluid(this.data.admissionId, {
        direction: v.direction,
        category: v.category,
        volumeMl: v.volumeMl!,
        measuredAt: this.form.controls.measuredAt.dirty ? fromLocalInput(v.measuredAt) : null,
        notes: v.notes.trim() || null,
      })
      .subscribe({
        next: (entry) => {
          this.notifications.success(`${entry.direction} of ${entry.volumeMl} ml recorded.`);
          this.dialogRef.close(entry);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}

/** Nursing assessment, care plan, progress or incident note. */
@Component({
  selector: 'app-nursing-note-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Nursing note</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Type</mat-label>
          <mat-select formControlName="kind">
            @for (k of kinds; track k) {
              <mat-option [value]="k">{{ labels[k] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Note (English or বাংলা)</mat-label>
          <textarea matInput formControlName="text" rows="6" maxlength="4000"></textarea>
          <mat-error><app-field-error [control]="form.controls.text" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save note</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
})
export class NursingNoteDialog {
  protected readonly data = inject<{ admissionId: string }>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<NursingNoteDialog, NursingNote>);
  private readonly api = inject(NursingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly kinds = Object.keys(NURSING_NOTE_LABELS) as NursingNoteKind[];
  protected readonly labels = NURSING_NOTE_LABELS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    kind: new FormControl<NursingNoteKind>('Progress', { nonNullable: true }),
    text: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
  });

  protected save(): void {
    if (this.form.invalid || !this.form.controls.text.value.trim()) {
      this.form.controls.text.setErrors({ required: true });
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api.addNote(this.data.admissionId, v.kind, v.text.trim()).subscribe({
      next: (note) => {
        this.notifications.success('Note saved.');
        this.dialogRef.close(note);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }
}

export interface RosterDialogData {
  wards: WardOption[];
  wardId: string | null;
  date: string;
  shift: Shift;
}

/** Puts a nurse (HR employee) on a ward shift. */
@Component({
  selector: 'app-roster-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatAutocompleteModule,
    MatCheckboxModule,
    MatDatepickerModule,
    FieldError,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Assign nurse</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field class="span">
          <mat-label>Nurse</mat-label>
          <input matInput [formControl]="search" [matAutocomplete]="auto" placeholder="Name or employee code" />
          <mat-autocomplete #auto="matAutocomplete" [displayWith]="display" (optionSelected)="form.controls.employee.setValue($event.option.value)">
            @for (e of employees(); track e.id) {
              <mat-option [value]="e">{{ e.fullName }} · {{ e.employeeCode }}{{ e.designationName ? ' · ' + e.designationName : '' }}</mat-option>
            }
          </mat-autocomplete>
          @if (form.controls.employee.touched && !form.controls.employee.value) {
            <mat-hint class="error-hint">Select a nurse from the list.</mat-hint>
          }
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Ward</mat-label>
          <mat-select formControlName="wardId">
            @for (w of data.wards; track w.id) {
              <mat-option [value]="w.id">{{ w.name }}</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.wardId" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Date</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="date" [min]="today" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Shift</mat-label>
          <mat-select formControlName="shift">
            @for (s of shifts; track s) {
              <mat-option [value]="s">{{ shiftLabels[s] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-checkbox class="span" formControlName="isInCharge">Nurse in charge of the shift</mat-checkbox>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Assign</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; } .error-hint { color: var(--mat-sys-error); }`,
})
export class RosterDialog {
  protected readonly data = inject<RosterDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RosterDialog, RosterEntry>);
  private readonly api = inject(NursingApiService);
  private readonly hr = inject(HrApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly shifts = SHIFTS;
  protected readonly shiftLabels = SHIFT_LABELS;
  protected readonly today = new Date(new Date().setHours(0, 0, 0, 0));
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly search = new FormControl<string | EmployeeLookupItem>('', { nonNullable: true });
  protected readonly form = new FormGroup({
    employee: new FormControl<EmployeeLookupItem | null>(null, Validators.required),
    wardId: new FormControl(this.data.wardId ?? '', { nonNullable: true, validators: Validators.required }),
    date: new FormControl<Date | null>(fromIsoDate(this.data.date), Validators.required),
    shift: new FormControl<Shift>(this.data.shift, { nonNullable: true }),
    isInCharge: new FormControl(false, { nonNullable: true }),
  });
  protected readonly employees = toSignal(
    this.search.valueChanges.pipe(
      startWith(''),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => (typeof term === 'string' ? this.hr.lookup(term.trim()).pipe(catchError(() => of([]))) : of([]))),
    ),
    { initialValue: [] as EmployeeLookupItem[] },
  );
  protected readonly display = (e: EmployeeLookupItem | string | null) => (e && typeof e !== 'string' ? e.fullName : (e ?? ''));

  constructor() {
    this.search.valueChanges.pipe(takeUntilDestroyed()).subscribe((v) => {
      if (typeof v === 'string') {
        this.form.controls.employee.setValue(null);
      }
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
    this.api
      .addRoster({ wardId: v.wardId, date: toIsoDate(v.date)!, shift: v.shift, employeeId: v.employee!.id, isInCharge: v.isInCharge })
      .subscribe({
        next: (entry) => {
          this.notifications.success(`${entry.employeeName} assigned to ${entry.wardName}.`);
          this.dialogRef.close(entry);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err));
        },
      });
  }
}
