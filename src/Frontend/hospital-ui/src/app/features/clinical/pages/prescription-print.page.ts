import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { BrandingService } from '../../../core/services/branding.service';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ageLabel } from '../../patient/patient.models';
import { ClinicalApiService, ConsultationRecord, durationLabel, TIMING_LABELS, vitalsSummary } from '../clinical.models';

/**
 * A4 prescription on the hospital letterhead (English; advice may be Bangla). Only finalized consultations print.
 * Use the browser's Print or "Save as PDF"; the app shell is hidden when printing.
 */
@Component({
  selector: 'app-prescription-print-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toolbar no-print">
      @if (record(); as r) {
        <a mat-button [routerLink]="['/opd/consultations', r.consultation.id]"><mat-icon>arrow_back</mat-icon> Back</a>
      }
      <button mat-flat-button type="button" (click)="print()" [disabled]="!printable()"><mat-icon>print</mat-icon> Print</button>
    </div>

    @if (error(); as message) {
      <div class="banner no-print" role="alert">{{ message }}</div>
    } @else if (record(); as r) {
      @if (!printable()) {
        <div class="banner no-print" role="alert">Only finalized consultations can be printed.</div>
      } @else {
        @let c = r.consultation;
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
            @if (r.doctor; as d) {
              <div class="doctor">
                <div class="doctor-name">{{ d.fullName }}</div>
                <div>{{ d.qualifications }}</div>
                <div>{{ d.specialization }}</div>
                <div>BMDC Reg. No. {{ d.registrationNumber }}</div>
              </div>
            }
          </header>

          <section class="patient">
            <span><strong>Name:</strong> {{ r.patient.fullName }}</span>
            <span><strong>Age/Sex:</strong> {{ age() }}{{ age() ? ' / ' : '' }}{{ r.patient.gender }}</span>
            <span><strong>MR No:</strong> {{ r.patient.mrNumber }}</span>
            <span><strong>Date:</strong> {{ c.encounterDate }}</span>
            <span><strong>Visit:</strong> {{ c.encounterNumber }}</span>
          </section>

          <div class="body">
            <div class="left">
              @if (c.chiefComplaint) {
                <h4>C/C</h4>
                <p>{{ c.chiefComplaint }}</p>
              }
              @if (r.latestVitals; as v) {
                <h4>O/E</h4>
                <p>{{ vitals(v) }}</p>
              }
              @if (c.examination) {
                <p class="pre">{{ c.examination }}</p>
              }
              <h4>Diagnosis</h4>
              @for (d of c.diagnoses; track d.id) {
                <p>{{ d.description }}</p>
              }
              @if (r.allergies.length) {
                <h4>Allergy</h4>
                <p>
                  @for (a of r.allergies; track a.allergen; let last = $last) {
                    {{ a.allergen }}{{ last ? '' : ', ' }}
                  }
                </p>
              }
              @if (c.investigations.length) {
                <h4>Investigations</h4>
                @for (i of c.investigations; track i.id) {
                  <p>• {{ i.name }}{{ i.notes ? ' (' + i.notes + ')' : '' }}</p>
                }
              }
            </div>

            <div class="right">
              <div class="rx-symbol">℞</div>
              <ol class="rx">
                @for (p of c.prescription; track p.id) {
                  <li>
                    <div class="med">{{ p.form ? p.form + ' ' : '' }}{{ p.medicineName }}{{ p.strength ? ' ' + p.strength : '' }}</div>
                    <div>
                      {{ p.dosePattern }} — {{ timing[p.timing] }} — {{ duration(p) }}{{ p.route && p.route !== 'Oral' ? ' — ' + p.route : '' }}{{ p.quantity ? ' (' + p.quantity + ')' : '' }}
                    </div>
                    @if (p.instructions) {
                      <div class="note">{{ p.instructions }}</div>
                    }
                  </li>
                }
              </ol>

              @if (c.advice) {
                <h4>Advice</h4>
                <p class="pre" lang="bn">{{ c.advice }}</p>
              }
              @if (c.followUpDate || c.followUpInstructions) {
                <h4>Follow-up</h4>
                <p>{{ c.followUpDate ? (c.followUpDate + ' ') : '' }}{{ c.followUpInstructions ?? '' }}</p>
              }
              @if (c.referralTo) {
                <h4>Referral</h4>
                <p>{{ c.referralTo }}{{ c.referralReason ? ' — ' + c.referralReason : '' }}</p>
              }
            </div>
          </div>

          <footer>
            <div class="printed">Finalized {{ c.finalizedAt | date: 'd MMM y, h:mm a' }}</div>
            <div class="signature">
              <div class="line"></div>
              <div>{{ r.doctor?.fullName }}</div>
            </div>
          </footer>
        </article>
      }
    }
  `,
  styles: `
    .toolbar { display: flex; gap: 8px; margin-bottom: 16px; }
    .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .page {
      box-sizing: border-box; width: 210mm; min-height: 297mm; margin: 0 auto; padding: 14mm 14mm 12mm;
      display: flex; flex-direction: column; background: #fff; color: #111; font: 13px/1.45 Roboto, Arial, sans-serif;
      box-shadow: 0 1px 6px rgb(0 0 0 / 0.2);
    }
    .letterhead { display: flex; justify-content: space-between; gap: 16px; padding-bottom: 8px; border-bottom: 2px solid #1a4b8c; }
    .hospital { display: flex; gap: 10px; align-items: center; }
    .logo { height: 56px; width: auto; }
    .name { font-size: 20px; font-weight: 600; color: #1a4b8c; }
    .contact { font-size: 11px; color: #444; max-width: 90mm; }
    .doctor { text-align: right; font-size: 12px; }
    .doctor-name { font-size: 15px; font-weight: 600; }
    .patient { display: flex; flex-wrap: wrap; gap: 4px 18px; padding: 6px 0; border-bottom: 1px solid #999; font-size: 12px; }
    .body { flex: 1; display: grid; grid-template-columns: 62mm 1fr; gap: 0 8mm; padding-top: 8px; }
    .left { padding-right: 6mm; border-right: 1px solid #ccc; font-size: 12px; }
    h4 { margin: 8px 0 2px; font-size: 12px; text-transform: uppercase; color: #1a4b8c; }
    p { margin: 0 0 2px; }
    .pre { white-space: pre-wrap; }
    .rx-symbol { font-size: 28px; font-weight: 600; color: #1a4b8c; }
    .rx { margin: 4px 0 8px; padding-left: 18px; }
    .rx li { margin-bottom: 8px; }
    .med { font-weight: 600; }
    .note { font-size: 12px; color: #444; }
    footer { display: flex; justify-content: space-between; align-items: flex-end; margin-top: 12mm; font-size: 11px; color: #444; }
    .signature { text-align: center; min-width: 55mm; color: #111; }
    .line { border-top: 1px solid #111; margin-bottom: 4px; }
    @media print {
      .page { width: auto; min-height: auto; margin: 0; padding: 0; box-shadow: none; }
      @page { size: A4; margin: 12mm; }
    }
  `,
})
export class PrescriptionPrintPage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(ClinicalApiService);
  protected readonly branding = inject(BrandingService);

  protected readonly timing = TIMING_LABELS;
  protected readonly duration = durationLabel;
  protected readonly vitals = vitalsSummary;
  protected readonly record = signal<ConsultationRecord | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly printable = computed(() => this.record()?.consultation.status === 'Finalized');
  protected readonly age = computed(() => {
    const dob = this.record()?.patient.dateOfBirth;
    return dob ? ageLabel(dob) : '';
  });
  protected readonly contactLine = computed(() => {
    const c = this.branding.contact();
    return [c.address, c.phone ? 'Phone: ' + c.phone : null, c.email].filter(Boolean).join(' · ');
  });

  ngOnInit(): void {
    this.api.consultation(this.id()).subscribe({
      next: (r) => this.record.set(r),
      error: (err) => this.error.set(apiErrorMessage(err, 'The prescription could not be loaded.')),
    });
  }

  protected print(): void {
    window.print();
  }
}
