import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ADMISSION_STATUS_LABELS, AdmissionSummary, IpdApiService } from '../ipd.models';

/** A patient's admissions, newest first (including records merged into this patient). Used on the patient profile. */
@Component({
  selector: 'app-patient-admissions-panel',
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <p class="secondary">{{ message }}</p>
    }
    @for (a of admissions(); track a.id) {
      <a class="row" [routerLink]="['/ipd/admissions', a.id]">
        <span class="grow">
          <strong>{{ a.admissionNumber }}</strong>
          <span class="badge" [class.current]="a.status !== 'Discharged'">{{ labels[a.status] }}</span>
          <span class="secondary">{{ a.doctorName }} · {{ a.wardName }} bed {{ a.bedNumber }}</span>
          @if (a.patientId !== patientId()) {
            <span class="secondary">· from merged record {{ a.patientMRNumber }}</span>
          }
        </span>
        <span class="secondary">
          {{ a.admittedAt | date: 'd MMM y' }} – {{ a.dischargedAt ? (a.dischargedAt | date: 'd MMM y') : 'now' }} · {{ a.days }} day{{ a.days === 1 ? '' : 's' }}
        </span>
      </a>
    } @empty {
      <p class="secondary">{{ loaded() ? 'No admissions.' : 'Loading…' }}</p>
    }
  `,
  styles: `
    .row { display: flex; flex-wrap: wrap; gap: 4px 12px; padding: 10px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); color: inherit; text-decoration: none; }
    .row:hover strong { text-decoration: underline; }
    .grow { flex: 1; display: flex; flex-wrap: wrap; gap: 4px 8px; align-items: baseline; }
    .badge { padding: 1px 8px; border-radius: 999px; font: var(--mat-sys-label-small); background: var(--mat-sys-surface-container-highest); }
    .badge.current { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class PatientAdmissionsPanel implements OnInit {
  readonly patientId = input.required<string>();

  private readonly api = inject(IpdApiService);

  protected readonly labels = ADMISSION_STATUS_LABELS;
  protected readonly admissions = signal<AdmissionSummary[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.admissions({ patientId: this.patientId(), currentOnly: false, pageNumber: 1, pageSize: 50 }).subscribe({
      next: (page) => {
        this.admissions.set(page.items);
        this.loaded.set(true);
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'Admissions could not be loaded.')),
    });
  }
}
