import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ageLabel } from '../../patient/patient.models';
import { ClinicalApiService, ConsultationRecord as Record_, vitalsSummary } from '../clinical.models';
import { ConsultationRecord } from '../components/consultation-record';

/** Read-only consultation from history. Route: /opd/consultations/:id. Viewing is audited by the API. */
@Component({
  selector: 'app-consultation-view-page',
  imports: [DatePipe, RouterLink, MatCardModule, MatButtonModule, MatIconModule, ConsultationRecord],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    } @else if (record(); as r) {
      <header class="head">
        <div>
          <div class="muted">{{ r.consultation.encounterNumber }} · {{ r.consultation.encounterDate }} · {{ r.doctor?.fullName }}</div>
          <h1><a [routerLink]="['/patients', r.patient.id]">{{ r.patient.fullName }}</a></h1>
          <div class="muted">{{ patientLine() }}</div>
        </div>
        <div class="actions">
          <a mat-stroked-button [routerLink]="['/opd/encounters', r.consultation.encounterId]"><mat-icon>open_in_new</mat-icon> Open visit</a>
          @if (r.consultation.status === 'Finalized') {
            <a mat-flat-button [routerLink]="['/opd/consultations', r.consultation.id, 'print']"><mat-icon>print</mat-icon> Print prescription</a>
          }
        </div>
      </header>
      @if (r.allergies.length) {
        <div class="banner" role="alert">
          <mat-icon>warning</mat-icon>
          <strong>Allergies:</strong>
          @for (a of r.allergies; track a.allergen; let last = $last) {
            {{ a.allergen }} ({{ a.severity }}){{ last ? '' : ',' }}
          }
        </div>
      }
      <mat-card appearance="outlined">
        <mat-card-header>
          <mat-card-title>Consultation</mat-card-title>
          <span class="badge">{{ r.consultation.status === 'Finalized' ? 'Finalized ' + (r.consultation.finalizedAt | date: 'd MMM y, h:mm a') : 'Draft' }}</span>
        </mat-card-header>
        <mat-card-content>
          @if (r.latestVitals; as v) {
            <p class="vitals"><strong>Vitals:</strong> {{ vitals(v) }}</p>
          }
          <app-consultation-record [consultation]="r.consultation" />
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    .head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; margin-bottom: 12px; }
    h1 { margin: 2px 0; font: var(--mat-sys-headline-small); }
    h1 a { color: inherit; text-decoration: none; }
    .actions { display: flex; gap: 8px; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .banner {
      display: flex; align-items: center; gap: 8px; padding: 10px 12px; margin-bottom: 12px; border-radius: 8px;
      background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container);
    }
    mat-card-header { align-items: center; }
    .badge {
      margin-left: auto; padding: 2px 10px; border-radius: 999px; font: var(--mat-sys-label-medium);
      background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container);
    }
    .vitals { margin: 8px 0 16px; }
  `,
})
export class ConsultationViewPage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(ClinicalApiService);

  protected readonly vitals = vitalsSummary;
  protected readonly record = signal<Record_ | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly patientLine = computed(() => {
    const p = this.record()?.patient;
    return p ? [p.mrNumber, [p.dateOfBirth ? ageLabel(p.dateOfBirth) : '', p.gender].filter(Boolean).join(' / ')].join(' · ') : '';
  });

  ngOnInit(): void {
    this.api.consultation(this.id()).subscribe({
      next: (r) => this.record.set(r),
      error: (err) => this.error.set(apiErrorMessage(err, 'The consultation could not be loaded.')),
    });
  }
}
