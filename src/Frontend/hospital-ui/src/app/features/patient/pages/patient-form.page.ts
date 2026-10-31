import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { PatientApiService } from '../patient-api.service';
import {
  Address,
  BLOOD_GROUPS,
  BloodGroup,
  DuplicateCandidate,
  Gender,
  GENDERS,
  ID_TYPES,
  IdentificationType,
  Patient,
  PatientDetails,
} from '../patient.models';

const MOBILE_PATTERN = /^\+?[0-9\s-]{6,20}$/;

type DuplicateQuery = {
  fullName: string | null;
  mobileNumber: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  identificationType: string | null;
  identificationNumber: string | null;
  excludePatientId: string | undefined;
};

/** Registration (route /patients/new) and edit (route /patients/:id/edit). */
@Component({
  selector: 'app-patient-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
    FieldError,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patient-form.page.html',
  styleUrl: './patient-form.page.scss',
})
export class PatientFormPage implements OnInit {
  /** Route parameter when editing. */
  readonly id = input<string>();

  private readonly api = inject(PatientApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly genders = GENDERS;
  protected readonly bloodGroups = BLOOD_GROUPS;
  protected readonly idTypes = ID_TYPES;
  protected readonly today = new Date();
  protected readonly districts = toSignal(this.api.districts().pipe(catchError(() => of([] as string[]))), { initialValue: [] });

  protected readonly patient = signal<Patient | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  protected readonly duplicates = signal<DuplicateCandidate[]>([]);
  protected readonly blocking = computed(() => this.duplicates().some((d) => d.isBlocking));

  protected readonly form = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(150)]],
    fullNameBangla: ['', Validators.maxLength(150)],
    gender: ['' as Gender | '', Validators.required],
    dobMode: ['dob' as 'dob' | 'age'],
    dateOfBirth: [null as Date | null],
    ageYears: [null as number | null, [Validators.min(0), Validators.max(129)]],
    ageMonths: [null as number | null, [Validators.min(0), Validators.max(11)]],
    bloodGroup: [null as BloodGroup | null],
    mobileNumber: ['', [Validators.required, Validators.pattern(MOBILE_PATTERN)]],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    presentAddress: this.addressGroup(),
    sameAsPresent: [true],
    permanentAddress: this.addressGroup(),
    identificationType: [null as IdentificationType | null],
    identificationNumber: ['', [Validators.maxLength(30), Validators.pattern(/^[A-Za-z0-9\s-]*$/)]],
    guardianName: ['', Validators.maxLength(150)],
    guardianRelation: ['', Validators.maxLength(50)],
    emergencyContacts: this.fb.array<FormGroup>([]),
  });

  protected readonly dobMode = toSignal(this.form.controls.dobMode.valueChanges, { initialValue: 'dob' as const });
  protected readonly sameAsPresent = toSignal(this.form.controls.sameAsPresent.valueChanges, { initialValue: true });

  get contacts(): FormArray<FormGroup> {
    return this.form.controls.emergencyContacts;
  }

  constructor() {
    // Live duplicate check while the receptionist types (warnings; an exact ID match blocks saving).
    this.form.valueChanges
      .pipe(
        debounceTime(500),
        map(() => this.duplicateQuery()),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        switchMap((q) =>
          (q.fullName?.length ?? 0) < 3 && !q.identificationNumber ? of([]) : this.api.duplicates(q).pipe(catchError(() => of([]))),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((candidates) => this.duplicates.set(candidates));
  }

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      return;
    }

    this.loading.set(true);
    this.api.getById(id).subscribe({
      next: (patient) => {
        this.patient.set(patient);
        this.fill(patient);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.bannerError.set(apiErrorMessage(error, 'The patient could not be loaded.'));
      },
    });
  }

  protected addContact(): void {
    this.contacts.push(this.contactGroup());
  }

  protected removeContact(index: number): void {
    this.contacts.removeAt(index);
  }

  protected save(): void {
    const dob = this.resolveDateOfBirth();
    if (this.form.invalid || !dob || this.saving()) {
      this.form.markAllAsTouched();
      if (!dob) {
        this.bannerError.set(this.dobMode() === 'age' ? 'Enter the age in years.' : 'Enter the date of birth.');
      }
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    const details = this.toDetails(dob.date, dob.estimated);
    const existing = this.patient();

    const request$ = existing ? this.api.update(existing.id, { ...details, rowVersion: existing.rowVersion }) : this.api.register(details);
    request$.subscribe({
      next: (saved) => {
        this.notifications.success(existing ? 'Patient updated.' : `Patient registered — MR number ${saved.mrNumber}.`);
        void this.router.navigate(['/patients', saved.id]);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }

  private addressGroup() {
    return this.fb.group({
      line: ['', Validators.maxLength(250)],
      upazila: ['', Validators.maxLength(100)],
      district: [null as string | null],
      postCode: ['', Validators.pattern(/^\d{4}$/)],
    });
  }

  private contactGroup(contact?: { name: string; relation: string; mobileNumber: string }) {
    return this.fb.group({
      name: [contact?.name ?? '', [Validators.required, Validators.maxLength(150)]],
      relation: [contact?.relation ?? '', [Validators.required, Validators.maxLength(50)]],
      mobileNumber: [contact?.mobileNumber ?? '', [Validators.required, Validators.pattern(MOBILE_PATTERN)]],
    });
  }

  /** Age entry derives an estimated date of birth (same day/month as today, minus the age). */
  private resolveDateOfBirth(): { date: string; estimated: boolean } | null {
    const v = this.form.getRawValue();
    if (v.dobMode === 'dob') {
      return v.dateOfBirth ? { date: toIsoDate(v.dateOfBirth)!, estimated: false } : null;
    }
    if (v.ageYears === null && v.ageMonths === null) {
      return null;
    }
    const date = new Date();
    date.setFullYear(date.getFullYear() - (v.ageYears ?? 0));
    date.setMonth(date.getMonth() - (v.ageMonths ?? 0));
    return { date: toIsoDate(date)!, estimated: true };
  }

  private duplicateQuery(): DuplicateQuery {
    const v = this.form.getRawValue();
    const dob = this.resolveDateOfBirth();
    return {
      fullName: v.fullName.trim() || null,
      mobileNumber: v.mobileNumber.trim() || null,
      dateOfBirth: dob && !dob.estimated ? dob.date : null,
      gender: v.gender || null,
      identificationType: v.identificationType,
      identificationNumber: v.identificationNumber.trim() || null,
      excludePatientId: this.patient()?.id,
    };
  }

  private toDetails(dateOfBirth: string, estimated: boolean): PatientDetails {
    const v = this.form.getRawValue();
    const blank = (s: string | null | undefined) => (s && s.trim() ? s.trim() : null);
    const address = (a: typeof v.presentAddress): Address => ({
      line: blank(a.line),
      upazila: blank(a.upazila),
      district: a.district,
      postCode: blank(a.postCode),
    });
    const present = address(v.presentAddress);

    return {
      fullName: v.fullName.trim(),
      fullNameBangla: blank(v.fullNameBangla),
      gender: v.gender as Gender,
      dateOfBirth,
      isDateOfBirthEstimated: estimated,
      bloodGroup: v.bloodGroup,
      mobileNumber: v.mobileNumber.trim(),
      email: blank(v.email),
      presentAddress: present,
      permanentAddress: v.sameAsPresent ? present : address(v.permanentAddress),
      identificationType: blank(v.identificationNumber) ? v.identificationType : null,
      identificationNumber: v.identificationType ? blank(v.identificationNumber) : null,
      guardianName: blank(v.guardianName),
      guardianRelation: blank(v.guardianRelation),
      emergencyContacts: this.contacts.getRawValue().map((c) => ({
        name: String(c['name']).trim(),
        relation: String(c['relation']).trim(),
        mobileNumber: String(c['mobileNumber']).trim(),
      })),
    };
  }

  private fill(p: Patient): void {
    const same = JSON.stringify(p.presentAddress) === JSON.stringify(p.permanentAddress);
    this.form.patchValue({
      fullName: p.fullName,
      fullNameBangla: p.fullNameBangla ?? '',
      gender: p.gender,
      dobMode: 'dob',
      dateOfBirth: fromIsoDate(p.dateOfBirth),
      bloodGroup: p.bloodGroup,
      mobileNumber: p.mobileNumber,
      email: p.email ?? '',
      presentAddress: { ...p.presentAddress, line: p.presentAddress.line ?? '', upazila: p.presentAddress.upazila ?? '', postCode: p.presentAddress.postCode ?? '' },
      sameAsPresent: same,
      permanentAddress: { ...p.permanentAddress, line: p.permanentAddress.line ?? '', upazila: p.permanentAddress.upazila ?? '', postCode: p.permanentAddress.postCode ?? '' },
      identificationType: p.identificationType,
      identificationNumber: p.identificationNumber ?? '',
      guardianName: p.guardianName ?? '',
      guardianRelation: p.guardianRelation ?? '',
    });
    this.contacts.clear();
    p.emergencyContacts.forEach((c) => this.contacts.push(this.contactGroup(c)));
  }
}
