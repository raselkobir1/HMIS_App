import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { filter, Observable, of, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ageLabel } from '../../patient/patient.models';
import {
  ClinicalApiService,
  ClinicalPermissions,
  Consultation,
  DiagnosisType,
  DURATION_UNITS,
  DurationUnit,
  MEDICINE_FORMS,
  MedicineTiming,
  OpdWorkspace,
  ROUTES,
  TIMING_LABELS,
  VitalSign,
  vitalsSummary,
} from '../clinical.models';
import { ConsultationRecord } from '../components/consultation-record';
import { VitalsDialog, VitalsDialogData } from '../components/vitals-dialog';

const DOSE_PATTERNS = ['1+0+1', '1+1+1', '1+0+0', '0+0+1', '0+1+0', '1+1+1+1', '½+0+½', 'SOS'];

const diagnosisGroup = (d?: { id: string | null; type: DiagnosisType; description: string }) =>
  new FormGroup({
    id: new FormControl<string | null>(d?.id ?? null),
    type: new FormControl<DiagnosisType>(d?.type ?? 'Secondary', { nonNullable: true }),
    description: new FormControl(d?.description ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(300)] }),
  });

type RxValues = Consultation['prescription'][number];
const rxGroup = (p?: RxValues) =>
  new FormGroup({
    id: new FormControl<string | null>(p?.id ?? null),
    form: new FormControl<string | null>(p?.form ?? 'Tab', Validators.maxLength(30)),
    medicineName: new FormControl(p?.medicineName ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(150)] }),
    strength: new FormControl<string | null>(p?.strength ?? null, Validators.maxLength(50)),
    route: new FormControl<string | null>(p?.route ?? 'Oral', Validators.maxLength(30)),
    dosePattern: new FormControl(p?.dosePattern ?? '1+0+1', { nonNullable: true, validators: [Validators.required, Validators.maxLength(30)] }),
    timing: new FormControl<MedicineTiming>(p?.timing ?? 'AfterFood', { nonNullable: true }),
    durationValue: new FormControl<number | null>(p ? p.durationValue : 7, [Validators.min(1), Validators.max(365)]),
    durationUnit: new FormControl<DurationUnit>(p?.durationUnit ?? 'Days', { nonNullable: true }),
    quantity: new FormControl<number | null>(p?.quantity ?? null, [Validators.min(1), Validators.max(10000)]),
    instructions: new FormControl<string | null>(p?.instructions ?? null, Validators.maxLength(300)),
  });

const investigationGroup = (i?: { id: string | null; name: string; notes: string | null }) =>
  new FormGroup({
    id: new FormControl<string | null>(i?.id ?? null),
    name: new FormControl(i?.name ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(150)] }),
    notes: new FormControl<string | null>(i?.notes ?? null, Validators.maxLength(300)),
  });

/**
 * OPD consultation workspace. Route: /opd/encounters/:encounterId (the checked-in appointment id; ?start=1 opens the
 * consultation straight away, used by My Queue). Patient context on the left, the doctor's record on the right.
 */
@Component({
  selector: 'app-consultation-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    FieldError,
    ConsultationRecord,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './consultation.page.html',
  styleUrl: './consultation.page.scss',
})
export class ConsultationPage implements OnInit {
  readonly encounterId = input.required<string>();
  readonly start = input<string>();

  private readonly api = inject(ClinicalApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly vitalsSummary = vitalsSummary;
  protected readonly timingLabels = TIMING_LABELS;
  protected readonly timings = Object.keys(TIMING_LABELS) as MedicineTiming[];
  protected readonly durationUnits = DURATION_UNITS;
  protected readonly forms = MEDICINE_FORMS;
  protected readonly routes = ROUTES;
  protected readonly dosePatterns = DOSE_PATTERNS;
  protected readonly can = {
    vitals: this.auth.hasPermission(ClinicalPermissions.vitalsRecord),
    write: this.auth.hasPermission(ClinicalPermissions.consultationManage),
  };

  protected readonly workspace = signal<OpdWorkspace | null>(null);
  protected readonly consultation = signal<Consultation | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly addendumText = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });

  protected readonly form = new FormGroup({
    chiefComplaint: new FormControl<string | null>(null, Validators.maxLength(1000)),
    historyOfPresentIllness: new FormControl<string | null>(null, Validators.maxLength(4000)),
    examination: new FormControl<string | null>(null, Validators.maxLength(4000)),
    advice: new FormControl<string | null>(null, Validators.maxLength(2000)),
    followUpDate: new FormControl<Date | null>(null),
    followUpInstructions: new FormControl<string | null>(null, Validators.maxLength(500)),
    referralTo: new FormControl<string | null>(null, Validators.maxLength(200)),
    referralReason: new FormControl<string | null>(null, Validators.maxLength(500)),
    diagnoses: new FormArray<ReturnType<typeof diagnosisGroup>>([]),
    prescription: new FormArray<ReturnType<typeof rxGroup>>([]),
    investigations: new FormArray<ReturnType<typeof investigationGroup>>([]),
  });

  protected readonly patientLine = computed(() => {
    const w = this.workspace();
    if (!w) return '';
    const age = w.patient.dateOfBirth ? ageLabel(w.patient.dateOfBirth) : '';
    return [w.patient.mrNumber, [age, w.patient.gender].filter(Boolean).join(' / '), w.patient.mobileNumber].join(' · ');
  });
  protected readonly validVitals = computed(() => (this.workspace()?.vitals ?? []).filter((v) => !v.isEnteredInError));
  protected readonly encounterActive = computed(() => ['Waiting', 'InConsultation'].includes(this.workspace()?.encounter.status ?? ''));
  protected readonly editable = computed(() => this.consultation()?.status === 'Draft' && !!this.workspace()?.canWrite && this.can.write);
  protected readonly canStart = computed(
    () => !this.consultation() && !!this.workspace()?.canWrite && this.can.write && this.encounterActive(),
  );
  protected readonly minFollowUp = computed(() => {
    const date = fromIsoDate(this.workspace()?.encounter.date);
    return date ? new Date(date.getFullYear(), date.getMonth(), date.getDate() + 1) : null;
  });

  get diagnoses() {
    return this.form.controls.diagnoses;
  }

  get prescription() {
    return this.form.controls.prescription;
  }

  get investigations() {
    return this.form.controls.investigations;
  }

  ngOnInit(): void {
    this.load(this.start() === '1');
  }

  /** True while the draft has unsaved edits (used by the leave-page guard). */
  hasUnsavedChanges(): boolean {
    return this.editable() && this.form.dirty;
  }

  protected load(openAfter = false): void {
    this.api.workspace(this.encounterId()).subscribe({
      next: (w) => {
        this.workspace.set(w);
        this.loadError.set(null);
        if (w.consultation) {
          this.setConsultation(w.consultation);
        } else if (openAfter && this.canStart()) {
          this.openConsultation();
        }
      },
      error: (err) => this.loadError.set(apiErrorMessage(err, 'The visit could not be loaded.')),
    });
  }

  protected openConsultation(): void {
    this.busy.set(true);
    this.api.openConsultation(this.encounterId()).subscribe({
      next: (c) => {
        this.busy.set(false);
        this.setConsultation(c);
        this.workspace.update((w) => (w ? { ...w, consultation: c, encounter: { ...w.encounter, status: 'InConsultation' } } : w));
      },
      error: (err) => {
        this.busy.set(false);
        this.notifications.error(apiErrorMessage(err));
      },
    });
  }

  protected recordVitals(): void {
    const w = this.workspace();
    if (!w) return;
    this.dialog
      .open<VitalsDialog, VitalsDialogData, VitalSign>(VitalsDialog, {
        data: { encounterId: w.encounter.id, patientLabel: `${w.patient.fullName} · ${this.patientLine()}` },
        width: '560px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe((v) => this.workspace.update((x) => (x ? { ...x, vitals: [v, ...x.vitals] } : x)));
  }

  protected addDiagnosis(): void {
    this.diagnoses.push(diagnosisGroup({ id: null, type: this.diagnoses.length === 0 ? 'Primary' : 'Secondary', description: '' }));
    this.form.markAsDirty();
  }

  /** Exactly one primary: choosing a new primary turns the previous one into a secondary diagnosis. */
  protected makePrimary(index: number): void {
    this.diagnoses.controls.forEach((g, i) => g.controls.type.setValue(i === index ? 'Primary' : 'Secondary'));
    this.form.markAsDirty();
  }

  protected addMedicine(): void {
    this.prescription.push(rxGroup());
    this.form.markAsDirty();
  }

  protected addInvestigation(): void {
    this.investigations.push(investigationGroup());
    this.form.markAsDirty();
  }

  protected remove(array: FormArray, index: number): void {
    array.removeAt(index);
    this.form.markAsDirty();
  }

  protected move(array: FormArray, index: number, delta: number): void {
    const target = index + delta;
    if (target < 0 || target >= array.length) return;
    const control = array.at(index);
    array.removeAt(index);
    array.insert(target, control);
    this.form.markAsDirty();
  }

  protected saveDraft(): void {
    this.persist().subscribe({ next: () => this.notifications.success('Draft saved.'), error: () => undefined });
  }

  protected finalize(): void {
    const primary = this.diagnoses.controls.find((g) => g.controls.type.value === 'Primary');
    if (!this.form.controls.chiefComplaint.value?.trim() || !primary) {
      this.error.set('Record the chief complaint and a primary diagnosis before finalizing.');
      return;
    }
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: 'Finalize consultation?',
          message: 'The record will be locked and the visit completed. Later corrections can only be added as addenda.',
          confirmText: 'Finalize & complete',
        },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.persist()),
        switchMap((c) => this.api.finalize(c.id, c.rowVersion)),
      )
      .subscribe({
        next: (c) => {
          this.busy.set(false);
          this.setConsultation(c);
          this.workspace.update((w) => (w ? { ...w, consultation: c, encounter: { ...w.encounter, status: 'Completed' } } : w));
          this.notifications.success('Consultation finalized. The visit is complete.');
        },
        error: (err) => {
          this.busy.set(false);
          if (err) this.error.set(apiErrorMessage(err));
        },
      });
  }

  protected addAddendum(): void {
    const c = this.consultation();
    if (!c || this.addendumText.invalid) {
      this.addendumText.markAsTouched();
      return;
    }
    this.busy.set(true);
    this.api.addAddendum(c.id, this.addendumText.value.trim()).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.consultation.set(updated);
        this.addendumText.reset();
        this.notifications.success('Addendum added.');
      },
      error: (err) => {
        this.busy.set(false);
        this.notifications.error(apiErrorMessage(err));
      },
    });
  }

  protected backToQueue(): void {
    void this.router.navigate(['/appointments/my-queue']);
  }

  protected ageOf(dateOfBirth: string | null): string {
    return dateOfBirth ? ageLabel(dateOfBirth) : '';
  }

  /** Saves the draft when there are changes; emits the latest consultation. Errors are shown in the banner. */
  private persist(): Observable<Consultation> {
    const c = this.consultation();
    if (!c) return of();
    if (!this.form.dirty) return of(c);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Some entries need attention (marked in red).');
      return new Observable<Consultation>((s) => s.error(null));
    }

    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    return new Observable<Consultation>((subscriber) => {
      this.api
        .save(c.id, {
          chiefComplaint: v.chiefComplaint,
          historyOfPresentIllness: v.historyOfPresentIllness,
          examination: v.examination,
          advice: v.advice,
          followUpDate: toIsoDate(v.followUpDate),
          followUpInstructions: v.followUpInstructions,
          referralTo: v.referralTo,
          referralReason: v.referralReason,
          diagnoses: v.diagnoses.map((d) => ({ ...d, description: d.description.trim() })),
          prescription: v.prescription.map((p) => ({
            ...p,
            medicineName: p.medicineName.trim(),
            durationValue: p.durationUnit === 'Continue' ? null : p.durationValue,
          })),
          investigations: v.investigations,
          rowVersion: c.rowVersion,
        })
        .subscribe({
          next: (saved) => {
            this.busy.set(false);
            this.setConsultation(saved);
            subscriber.next(saved);
            subscriber.complete();
          },
          error: (err) => {
            this.busy.set(false);
            this.error.set(
              err?.status === 409
                ? 'This consultation was changed in another window. Reload the page to see the latest version.'
                : apiErrorMessage(err, 'The draft could not be saved.'),
            );
            subscriber.error(null);
          },
        });
    });
  }

  private setConsultation(c: Consultation): void {
    this.consultation.set(c);
    this.form.patchValue({
      chiefComplaint: c.chiefComplaint,
      historyOfPresentIllness: c.historyOfPresentIllness,
      examination: c.examination,
      advice: c.advice,
      followUpDate: fromIsoDate(c.followUpDate),
      followUpInstructions: c.followUpInstructions,
      referralTo: c.referralTo,
      referralReason: c.referralReason,
    });
    this.diagnoses.clear();
    c.diagnoses.forEach((d) => this.diagnoses.push(diagnosisGroup(d)));
    this.prescription.clear();
    c.prescription.forEach((p) => this.prescription.push(rxGroup(p)));
    this.investigations.clear();
    c.investigations.forEach((i) => this.investigations.push(investigationGroup(i)));
    this.form.markAsPristine();
  }
}
