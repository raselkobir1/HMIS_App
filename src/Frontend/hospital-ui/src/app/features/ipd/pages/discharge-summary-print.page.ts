import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { BrandingService } from '../../../core/services/branding.service';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ageLabel } from '../../patient/patient.models';
import { DISCHARGE_TYPE_LABELS, DischargeSummary, IpdApiService } from '../ipd.models';

/** A4 discharge summary on the hospital letterhead. Opening it is audited. Route: /ipd/admissions/:id/summary. */
@Component({
  selector: 'app-discharge-summary-print-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toolbar no-print">
      <a mat-button [routerLink]="['/ipd/admissions', id()]"><mat-icon>arrow_back</mat-icon> Back</a>
      <button mat-flat-button type="button" (click)="print()" [disabled]="!summary()"><mat-icon>print</mat-icon> Print</button>
    </div>
    @if (error(); as message) {
      <div class="banner no-print" role="alert">{{ message }}</div>
    } @else if (summary(); as s) {
      <article class="page print-area">
        <header class="letterhead">
          <div class="hospital">
            @if (branding.logoUrl(); as logo) {
              <img [src]="logo" alt="" class="logo" />
            }
            <div>
              <div class="name">{{ branding.name() }}</div>
              <div class="contact">{{ contactLine() }}</div>
            </div>
          </div>
          <div class="title">Discharge summary</div>
        </header>

        <section class="meta">
          <span><strong>Name:</strong> {{ s.patient?.fullName }}</span>
          <span><strong>Age/Sex:</strong> {{ age() }}{{ age() ? ' / ' : '' }}{{ s.patient?.gender }}</span>
          <span><strong>MR No:</strong> {{ s.patient?.mrNumber }}</span>
          <span><strong>Admission:</strong> {{ s.admissionNumber }}</span>
          <span><strong>Ward/Bed:</strong> {{ s.wardName }} {{ s.bedNumber }}</span>
          <span><strong>Admitted:</strong> {{ s.admittedAt | date: 'd MMM y, h:mm a' }}</span>
          <span><strong>Discharged:</strong> {{ s.dischargedAt ? (s.dischargedAt | date: 'd MMM y, h:mm a') : '—' }}</span>
          @if (s.releasedAt) {
            <span><strong>Left ward:</strong> {{ s.releasedAt | date: 'd MMM y, h:mm a' }}</span>
          }
          <span><strong>Type:</strong> {{ types[s.discharge.type] }}</span>
        </section>

        <h4>Reason for admission</h4>
        <p>{{ s.reason }}</p>
        <h4>Final diagnosis</h4>
        <p>{{ s.discharge.finalDiagnosis }}</p>
        @if (s.discharge.hospitalCourse) {
          <h4>Hospital course and treatment</h4>
          <p class="pre">{{ s.discharge.hospitalCourse }}</p>
        }
        @if (s.discharge.dischargeMedications) {
          <h4>Medicines at discharge</h4>
          <p class="pre">{{ s.discharge.dischargeMedications }}</p>
        }
        @if (s.discharge.advice) {
          <h4>Advice</h4>
          <p class="pre" lang="bn">{{ s.discharge.advice }}</p>
        }
        @if (s.discharge.followUpDate) {
          <h4>Follow-up</h4>
          <p>{{ s.discharge.followUpDate }}</p>
        }

        <footer>
          <div>Printed {{ now | date: 'd MMM y, h:mm a' }}</div>
          @if (s.doctor; as d) {
            <div class="signature">
              <div class="line"></div>
              <div class="doctor">{{ d.fullName }}</div>
              <div>{{ d.qualifications }}</div>
              <div>{{ s.departmentName ?? d.specialization }}</div>
              <div>BMDC Reg. No. {{ d.registrationNumber }}</div>
            </div>
          }
        </footer>
      </article>
    }
  `,
  styles: `
    .toolbar { display: flex; gap: 8px; margin-bottom: 16px; }
    .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .page {
      box-sizing: border-box; width: 210mm; min-height: 297mm; margin: 0 auto; padding: 14mm; display: flex; flex-direction: column;
      background: #fff; color: #111; font: 13px/1.45 Roboto, Arial, sans-serif; box-shadow: 0 1px 6px rgb(0 0 0 / 0.2);
    }
    .letterhead { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; padding-bottom: 8px; border-bottom: 2px solid #1a4b8c; }
    .hospital { display: flex; gap: 10px; align-items: center; }
    .logo { height: 56px; width: auto; }
    .name { font-size: 20px; font-weight: 600; color: #1a4b8c; }
    .contact { font-size: 11px; color: #444; max-width: 100mm; }
    .title { font-size: 16px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; }
    .meta { display: grid; grid-template-columns: 1fr 1fr; gap: 2px 18px; padding: 6px 0; border-bottom: 1px solid #999; font-size: 12px; }
    h4 { margin: 10px 0 2px; font-size: 12px; text-transform: uppercase; color: #1a4b8c; }
    p { margin: 0; }
    .pre { white-space: pre-wrap; }
    footer { display: flex; justify-content: space-between; align-items: flex-end; margin-top: auto; padding-top: 16mm; font-size: 11px; color: #444; }
    .signature { text-align: center; min-width: 60mm; color: #111; }
    .doctor { font-weight: 600; }
    .line { border-top: 1px solid #111; margin-bottom: 4px; }
    @media print {
      .page { width: auto; min-height: auto; margin: 0; padding: 0; box-shadow: none; }
      @page { size: A4; margin: 12mm; }
    }
  `,
})
export class DischargeSummaryPrintPage implements OnInit {
  readonly id = input.required<string>();
  private readonly api = inject(IpdApiService);
  protected readonly branding = inject(BrandingService);

  protected readonly types = DISCHARGE_TYPE_LABELS;
  protected readonly now = new Date();
  protected readonly summary = signal<DischargeSummary | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly age = computed(() => {
    const dob = this.summary()?.patient?.dateOfBirth;
    return dob ? ageLabel(dob) : '';
  });
  protected readonly contactLine = computed(() => {
    const c = this.branding.contact();
    return [c.address, c.phone ? 'Phone: ' + c.phone : null, c.email].filter(Boolean).join(' · ');
  });

  ngOnInit(): void {
    this.api.dischargeSummary(this.id()).subscribe({
      next: (s) => this.summary.set(s),
      error: (err) => this.error.set(apiErrorMessage(err, 'The discharge summary could not be loaded.')),
    });
  }

  protected print(): void {
    window.print();
  }
}
