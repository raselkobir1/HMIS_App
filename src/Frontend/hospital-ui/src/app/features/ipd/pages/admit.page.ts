import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { map } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { TakaPipe } from '../../billing/components/taka.pipe';
import { Doctor, DoctorApiService } from '../../hr/doctors.models';
import { PatientPicker } from '../../patient/components/patient-picker';
import { PatientApiService } from '../../patient/patient-api.service';
import { PatientListItem } from '../../patient/patient.models';
import { AdmissionSource, Bed, groupBeds, IpdApiService, SOURCE_LABELS } from '../ipd.models';

/** Admit a patient to a free bed. Route: /ipd/admit (optional ?patientId= and ?bedId=). */
@Component({
  selector: 'app-admit-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    PageHeader,
    PatientPicker,
    FieldError,
    TakaPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Admit patient" subtitle="Admission is never held up by payment; the deposit is collected at the cash counter" />
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    <form [formGroup]="form" (ngSubmit)="admit()" novalidate>
      <mat-card appearance="outlined">
        <mat-card-content class="grid">
          <div class="span"><app-patient-picker [(patient)]="patient" /></div>
          <mat-form-field>
            <mat-label>Attending doctor</mat-label>
            <mat-select formControlName="attendingDoctorId">
              @for (d of doctors(); track d.employeeId) {
                <mat-option [value]="d.employeeId">{{ d.fullName }} — {{ d.specialization }}</mat-option>
              }
            </mat-select>
            <mat-error><app-field-error [control]="form.controls.attendingDoctorId" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Bed</mat-label>
            <mat-select formControlName="bedId">
              @for (g of bedGroups(); track g.ward) {
                <mat-optgroup [label]="g.ward">
                  @for (b of g.beds; track b.id) {
                    <mat-option [value]="b.id">
                      {{ b.bedNumber }}{{ b.room ? ' (room ' + b.room + ')' : '' }} · {{ b.categoryName }}{{ b.status === 'Reserved' ? ' · reserved' : '' }}
                    </mat-option>
                  }
                </mat-optgroup>
              } @empty {
                <mat-option disabled>No free beds</mat-option>
              }
            </mat-select>
            @if (selectedBed(); as b) {
              <mat-hint>{{ b.dailyRate | taka }} per day{{ b.statusNote ? ' · ' + b.statusNote : '' }}</mat-hint>
            }
            <mat-error><app-field-error [control]="form.controls.bedId" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Admitted from</mat-label>
            <mat-select formControlName="source">
              @for (s of sources; track s) {
                <mat-option [value]="s">{{ sourceLabels[s] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Reference (optional)</mat-label>
            <input matInput formControlName="sourceReference" maxlength="50" placeholder="e.g. appointment or referral number" />
          </mat-form-field>
          <mat-form-field class="span">
            <mat-label>Reason for admission</mat-label>
            <textarea matInput formControlName="reason" rows="2" maxlength="500"></textarea>
            <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
          </mat-form-field>
          <h3 class="span">Emergency contact</h3>
          <mat-form-field>
            <mat-label>Name</mat-label>
            <input matInput formControlName="emergencyContactName" maxlength="100" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Relation</mat-label>
            <input matInput formControlName="emergencyContactRelation" maxlength="50" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Phone</mat-label>
            <input matInput formControlName="emergencyContactPhone" maxlength="20" inputmode="tel" />
            <mat-error><app-field-error [control]="form.controls.emergencyContactPhone" /></mat-error>
          </mat-form-field>
        </mat-card-content>
        <mat-card-actions align="end">
          @if (selectedDeposit() > 0) {
            <span class="deposit">Suggested deposit: <strong>{{ selectedDeposit() | taka }}</strong></span>
          }
          <button mat-flat-button type="submit" [disabled]="saving() || !patient()"><mat-icon>hotel</mat-icon> Admit</button>
        </mat-card-actions>
      </mat-card>
    </form>
  `,
  styles: `
    .banner { padding: 12px; margin-bottom: 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 12px; padding-top: 8px; }
    .span { grid-column: 1 / -1; }
    h3 { margin: 4px 0 8px; font: var(--mat-sys-title-small); color: var(--mat-sys-on-surface-variant); }
    .deposit { margin-right: auto; padding-left: 8px; }
    @media (max-width: 700px) { .grid { grid-template-columns: 1fr; } }
  `,
})
export class AdmitPage implements OnInit {
  readonly patientId = input<string>();
  readonly bedId = input<string>();

  private readonly api = inject(IpdApiService);
  private readonly patientApi = inject(PatientApiService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly picker = viewChild(PatientPicker);

  protected readonly sources = Object.keys(SOURCE_LABELS) as AdmissionSource[];
  protected readonly sourceLabels = SOURCE_LABELS;
  protected readonly patient = signal<PatientListItem | null>(null);
  protected readonly beds = signal<Bed[]>([]);
  protected readonly deposits = signal<Record<string, number>>({});
  protected readonly bedGroups = computed(() => groupBeds(this.beds()));
  protected readonly doctors = toSignal(
    inject(DoctorApiService)
      .search({ pageNumber: 1, pageSize: 100 })
      .pipe(map((page) => page.items.filter((d) => d.employmentStatus === 'Active' || d.employmentStatus === 'OnLeave'))),
    { initialValue: [] as Doctor[] },
  );
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = new FormGroup({
    attendingDoctorId: new FormControl('', { nonNullable: true, validators: Validators.required }),
    bedId: new FormControl('', { nonNullable: true, validators: Validators.required }),
    source: new FormControl<AdmissionSource>('Opd', { nonNullable: true }),
    sourceReference: new FormControl<string | null>(null),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }),
    emergencyContactName: new FormControl<string | null>(null),
    emergencyContactRelation: new FormControl<string | null>(null),
    emergencyContactPhone: new FormControl<string | null>(null, Validators.pattern(/^\+?[0-9 -]{6,20}$/)),
  });

  private readonly bedValue = toSignal(this.form.controls.bedId.valueChanges, { initialValue: '' });
  protected readonly selectedBed = computed(() => this.beds().find((b) => b.id === this.bedValue()) ?? null);
  protected readonly selectedDeposit = computed(() => {
    const bed = this.selectedBed();
    return bed ? (this.deposits()[bed.categoryId] ?? 0) : 0;
  });

  ngOnInit(): void {
    this.api.beds({ assignableOnly: true }).subscribe((beds) => {
      this.beds.set(beds);
      const preset = this.bedId();
      if (preset && beds.some((b) => b.id === preset)) {
        this.form.controls.bedId.setValue(preset);
      }
    });
    this.api.categories().subscribe((list) => this.deposits.set(Object.fromEntries(list.map((c) => [c.id, c.suggestedDeposit]))));
    const patientId = this.patientId();
    if (patientId) {
      this.patientApi.getById(patientId).subscribe((p) =>
        this.picker()?.setPatient({
          id: p.id,
          mrNumber: p.mrNumber,
          fullName: p.fullName,
          gender: p.gender,
          dateOfBirth: p.dateOfBirth,
          isDateOfBirthEstimated: p.isDateOfBirthEstimated,
          mobileNumber: p.mobileNumber,
          district: null,
          status: p.status,
        }),
      );
    }
  }

  protected admit(): void {
    const patient = this.patient();
    if (this.form.invalid || !patient) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const clean = (value: string | null) => value?.trim() || null;
    this.saving.set(true);
    this.error.set(null);
    this.api
      .admit({
        patientId: patient.id,
        attendingDoctorId: v.attendingDoctorId,
        bedId: v.bedId,
        source: v.source,
        sourceReference: clean(v.sourceReference),
        reason: v.reason.trim(),
        emergencyContactName: clean(v.emergencyContactName),
        emergencyContactRelation: clean(v.emergencyContactRelation),
        emergencyContactPhone: clean(v.emergencyContactPhone),
      })
      .subscribe({
        next: (a) => {
          this.notifications.success(`${a.summary.patientName} admitted to bed ${a.summary.bedNumber} (${a.summary.admissionNumber}).`);
          void this.router.navigate(['/ipd/admissions', a.summary.id]);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
          // A bed taken by someone else meanwhile: refresh the list.
          this.api.beds({ assignableOnly: true }).subscribe((beds) => this.beds.set(beds));
        },
      });
  }
}
