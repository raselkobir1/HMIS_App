import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { PatientAppointmentsPanel } from '../../appointment/components/patient-appointments-panel';
import { AppointmentPermissions } from '../../appointment/appointment.models';
import { DocumentsPanel } from '../components/documents-panel';
import { MedicalInfoPanel } from '../components/medical-info-panel';
import { PatientApiService } from '../patient-api.service';
import { Address, ageLabel, bloodGroupLabel, idTypeLabel, MedicalInfo, Patient, PatientPermissions } from '../patient.models';

@Component({
  selector: 'app-patient-profile-page',
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MedicalInfoPanel,
    DocumentsPanel,
    PatientAppointmentsPanel,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patient-profile.page.html',
  styleUrl: './patient-profile.page.scss',
})
export class PatientProfilePage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(PatientApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly can = {
    update: this.auth.hasPermission(PatientPermissions.update),
    deactivate: this.auth.hasPermission(PatientPermissions.deactivate),
    mergeRequest: this.auth.hasPermission(PatientPermissions.mergeRequest),
    medicalView: this.auth.hasPermission(PatientPermissions.medicalView),
    medicalManage: this.auth.hasPermission(PatientPermissions.medicalManage),
    documentsView: this.auth.hasPermission(PatientPermissions.documentsView),
    documentsUpload: this.auth.hasPermission(PatientPermissions.documentsUpload),
    documentsDelete: this.auth.hasPermission(PatientPermissions.documentsDelete),
    appointmentsView: this.auth.hasPermission(AppointmentPermissions.view),
    book: this.auth.hasPermission(AppointmentPermissions.book),
  };

  protected readonly patient = signal<Patient | null>(null);
  protected readonly medical = signal<MedicalInfo | null>(null);
  protected readonly photoUrl = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly editable = computed(() => this.patient()?.status === 'Active');
  protected readonly activeAllergies = computed(() => (this.medical()?.allergies ?? []).filter((a) => a.status === 'Active'));
  protected readonly age = computed(() => {
    const p = this.patient();
    return p ? ageLabel(p.dateOfBirth, p.isDateOfBirthEstimated) : '';
  });
  protected readonly bloodGroup = computed(() => bloodGroupLabel(this.patient()?.bloodGroup));
  protected readonly idType = computed(() => idTypeLabel(this.patient()?.identificationType));

  constructor() {
    inject(DestroyRef).onDestroy(() => this.revokePhoto());
  }

  ngOnInit(): void {
    this.load();
  }

  protected formatAddress(a: Address): string {
    return [a.line, a.upazila, a.district, a.postCode].filter((part) => !!part).join(', ') || '—';
  }

  protected loadMedical(): void {
    if (!this.can.medicalView) {
      return;
    }
    this.api.medicalInfo(this.id()).subscribe({ next: (m) => this.medical.set(m), error: () => this.medical.set(null) });
  }

  protected uploadPhoto(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    this.api.uploadPhoto(this.id(), file).subscribe({
      next: (p) => {
        this.patient.set(p);
        this.loadPhoto();
        this.notifications.success('Photo updated.');
      },
      error: (err) => this.notifications.error(apiErrorMessage(err, 'The photo could not be uploaded.')),
    });
  }

  protected toggleStatus(): void {
    const p = this.patient();
    if (!p) {
      return;
    }
    const activate = p.status !== 'Active';
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: activate ? 'Reactivate record' : 'Deactivate record',
          message: activate
            ? `Reactivate ${p.fullName} (${p.mrNumber})?`
            : `Deactivate ${p.fullName} (${p.mrNumber})? The record is kept but cannot be changed until reactivated.`,
          confirmText: activate ? 'Reactivate' : 'Deactivate',
          destructive: !activate,
        },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.setStatus(p.id, activate)),
      )
      .subscribe({
        next: (updated) => this.patient.set(updated),
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }

  protected requestMerge(): void {
    const p = this.patient();
    if (!p) {
      return;
    }
    this.dialog.open(MergeRequestDialog, { data: p, width: '480px', maxWidth: '95vw' });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getById(this.id()).subscribe({
      next: (p) => {
        this.patient.set(p);
        this.loading.set(false);
        this.loadPhoto();
        this.loadMedical();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(err, 'The patient could not be loaded.'));
      },
    });
  }

  /** The photo endpoint requires the session token, so it is loaded as a blob rather than a plain <img src>. */
  private loadPhoto(): void {
    this.revokePhoto();
    if (!this.patient()?.hasPhoto) {
      return;
    }
    this.api.photo(this.id()).subscribe({ next: (blob) => this.photoUrl.set(URL.createObjectURL(blob)), error: () => undefined });
  }

  private revokePhoto(): void {
    const url = this.photoUrl();
    if (url) {
      URL.revokeObjectURL(url);
      this.photoUrl.set(null);
    }
  }
}

/** Marks this patient as a duplicate of another (surviving) record; a Hospital Administrator approves it. */
@Component({
  selector: 'app-merge-request-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Report duplicate record</h2>
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <mat-dialog-content>
        <p>
          <strong>{{ source.mrNumber }} · {{ source.fullName }}</strong> will be merged into the record below after approval.
          It is kept, read-only, and points to the surviving record.
        </p>
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Surviving record MR number</mat-label>
          <input matInput formControlName="targetMrn" (blur)="lookup()" placeholder="e.g. MR26000012" />
          @if (target(); as t) {
            <mat-hint>{{ t.fullName }} · {{ t.gender }} · {{ t.mobileNumber }}</mat-hint>
          }
          <mat-error><app-field-error [control]="form.controls.targetMrn" /></mat-error>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Reason</mat-label>
          <textarea matInput formControlName="reason" rows="2" placeholder="e.g. Same person registered twice (verified NID)"></textarea>
          <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving() || !target()">Submit for approval</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
})
export class MergeRequestDialog {
  protected readonly source = inject<Patient>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<MergeRequestDialog>);
  private readonly api = inject(PatientApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly target = signal<Patient | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    targetMrn: ['', Validators.required],
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });

  protected lookup(): void {
    const mrn = this.form.controls.targetMrn.value.trim();
    this.target.set(null);
    if (!mrn) {
      return;
    }
    this.api.getByMRNumber(mrn).subscribe({
      next: (p) => {
        if (p.id === this.source.id) {
          this.form.controls.targetMrn.setErrors({ server: 'Choose a different patient.' });
        } else {
          this.target.set(p);
        }
      },
      error: () => this.form.controls.targetMrn.setErrors({ server: 'No patient with this MR number.' }),
    });
  }

  protected submit(): void {
    const target = this.target();
    if (this.form.invalid || !target) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.api.requestMerge(this.source.id, target.id, this.form.controls.reason.value.trim()).subscribe({
      next: () => {
        this.notifications.success('Merge request submitted for approval.');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }
}
