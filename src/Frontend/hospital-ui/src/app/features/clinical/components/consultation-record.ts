import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { Consultation, durationLabel, TIMING_LABELS } from '../clinical.models';

/** Read-only consultation (finalized record, or a draft seen by someone who cannot edit it). */
@Component({
  selector: 'app-consultation-record',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let c = consultation();
    <section>
      <h3>Chief complaint</h3>
      <p>{{ c.chiefComplaint ?? '—' }}</p>
      @if (c.historyOfPresentIllness) {
        <h3>History of present illness</h3>
        <p class="pre">{{ c.historyOfPresentIllness }}</p>
      }
      @if (c.examination) {
        <h3>Examination</h3>
        <p class="pre">{{ c.examination }}</p>
      }
      <h3>Diagnosis</h3>
      @for (d of c.diagnoses; track d.id) {
        <p>{{ d.description }} @if (d.type === 'Primary') {<span class="tag">Primary</span>}</p>
      } @empty {
        <p class="muted">None recorded</p>
      }
      <h3>Prescription</h3>
      @if (c.prescription.length) {
        <ol class="rx">
          @for (p of c.prescription; track p.id) {
            <li>
              <strong>{{ p.form ? p.form + ' ' : '' }}{{ p.medicineName }}{{ p.strength ? ' ' + p.strength : '' }}</strong>
              <div>{{ p.dosePattern }} · {{ timing[p.timing] }} · {{ duration(p) }}{{ p.route && p.route !== 'Oral' ? ' · ' + p.route : '' }}{{ p.quantity ? ' · Qty ' + p.quantity : '' }}</div>
              @if (p.instructions) {
                <div class="muted">{{ p.instructions }}</div>
              }
            </li>
          }
        </ol>
      } @else {
        <p class="muted">No medicines</p>
      }
      @if (c.investigations.length) {
        <h3>Investigations</h3>
        <ul>
          @for (i of c.investigations; track i.id) {
            <li>{{ i.name }}{{ i.notes ? ' — ' + i.notes : '' }}</li>
          }
        </ul>
      }
      @if (c.advice) {
        <h3>Advice</h3>
        <p class="pre">{{ c.advice }}</p>
      }
      @if (c.followUpDate || c.followUpInstructions) {
        <h3>Follow-up</h3>
        <p>{{ c.followUpDate ?? '' }}{{ c.followUpDate && c.followUpInstructions ? ' — ' : '' }}{{ c.followUpInstructions ?? '' }}</p>
      }
      @if (c.referralTo) {
        <h3>Referral</h3>
        <p>{{ c.referralTo }}{{ c.referralReason ? ' — ' + c.referralReason : '' }}</p>
      }
      @if (c.addenda.length) {
        <h3>Addenda</h3>
        @for (a of c.addenda; track a.id) {
          <div class="addendum">
            <div class="muted">{{ a.createdAt | date: 'd MMM y, h:mm a' }} · {{ a.createdBy ?? 'Unknown' }}</div>
            <p class="pre">{{ a.text }}</p>
          </div>
        }
      }
    </section>
  `,
  styles: `
    h3 { margin: 16px 0 4px; font: var(--mat-sys-title-small); color: var(--mat-sys-primary); }
    h3:first-child { margin-top: 0; }
    p { margin: 0 0 4px; }
    .pre { white-space: pre-wrap; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .tag {
      margin-left: 6px; padding: 1px 8px; border-radius: 999px; font: var(--mat-sys-label-small);
      background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container);
    }
    .rx { margin: 0; padding-left: 20px; }
    .rx li { margin-bottom: 6px; }
    .addendum { padding: 8px 12px; margin-bottom: 8px; border-left: 3px solid var(--mat-sys-tertiary); background: var(--mat-sys-surface-container-low); }
  `,
})
export class ConsultationRecord {
  readonly consultation = input.required<Consultation>();

  protected readonly timing = TIMING_LABELS;
  protected readonly duration = durationLabel;
}
