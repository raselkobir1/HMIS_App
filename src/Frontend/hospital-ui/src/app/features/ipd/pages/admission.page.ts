import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { filter, Observable, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { BillingPermissions } from '../../billing/billing.models';
import { ReasonDialog } from '../../billing/components/reason-dialog';
import { TakaPipe } from '../../billing/components/taka.pipe';
import { DischargeOrderDialog, DoctorDialog, TransferDialog } from '../components/admission-dialogs';
import { Admission, ADMISSION_STATUS_LABELS, DISCHARGE_TYPE_LABELS, IpdApiService, IpdPermissions, SOURCE_LABELS } from '../ipd.models';

/** One stay: bed, doctor, bill and deposit, discharge summary and every action by status. Route: /ipd/admissions/:id. */
@Component({
  selector: 'app-admission-page',
  imports: [DatePipe, RouterLink, MatCardModule, MatButtonModule, MatIconModule, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <div class="banner error" role="alert">{{ message }}</div>
    } @else if (admission(); as a) {
      @let s = a.summary;
      <header class="head">
        <div>
          <a mat-button routerLink="/ipd/admissions" class="back"><mat-icon>arrow_back</mat-icon> Admissions</a>
          <h1>{{ s.patientName }} <span class="badge" [class]="'badge ' + s.status">{{ labels[s.status] }}</span></h1>
          <div class="sub">
            <a [routerLink]="['/patients', s.patientId]">{{ s.patientMRNumber }}</a> · {{ s.admissionNumber }} ·
            {{ s.wardName }} bed {{ s.bedNumber }}
          </div>
        </div>
        <div class="actions">
          @if (inBed()) {
            @if (can.dischargeOrder) {
              <button mat-flat-button type="button" (click)="orderDischarge(a)">
                <mat-icon>assignment_turned_in</mat-icon> {{ s.status === 'Admitted' ? 'Order discharge' : 'Update summary' }}
              </button>
              @if (s.status === 'DischargeOrdered') {
                <button mat-button type="button" (click)="cancelOrder(a)">Cancel discharge order</button>
              }
            }
            @if (can.complete && s.status === 'DischargeOrdered') {
              <button mat-flat-button type="button" [disabled]="busy()" (click)="prepareFinalBill(a)"><mat-icon>receipt_long</mat-icon> Prepare final bill</button>
            }
            @if (can.admit) {
              <button mat-stroked-button type="button" (click)="transfer(a)"><mat-icon>swap_horiz</mat-icon> Transfer bed</button>
              <button mat-stroked-button type="button" (click)="changeDoctor(a)"><mat-icon>stethoscope</mat-icon> Change doctor</button>
            }
          }
          @if (s.status === 'FinalBilled') {
            @if (can.dischargeOrder) {
              <button mat-stroked-button type="button" (click)="orderDischarge(a)"><mat-icon>edit_note</mat-icon> Update summary</button>
            }
            @if (can.complete) {
              @if (due() <= 0) {
                <button mat-flat-button type="button" [disabled]="busy()" (click)="release(a, null)"><mat-icon>logout</mat-icon> Discharge patient</button>
              } @else if (can.override) {
                <button mat-flat-button type="button" class="warn" [disabled]="busy()" (click)="releaseWithDue(a)">
                  <mat-icon>logout</mat-icon> Discharge with due
                </button>
              }
            }
          }
          @if (a.discharge) {
            <a mat-stroked-button [routerLink]="['/ipd/admissions', s.id, 'summary']"><mat-icon>print</mat-icon> Discharge summary</a>
          }
        </div>
      </header>

      @if (s.status === 'FinalBilled' && due() > 0) {
        <div class="banner warn" role="status">
          <mat-icon>payments</mat-icon>
          {{ a.bill?.dueAmount | taka }} is due on the final bill. Settle it at the cash counter{{ a.advanceBalance > 0 ? ' (the deposit of ' + (a.advanceBalance | taka) + ' can be applied)' : '' }} before discharge{{ can.override ? ', or discharge with due and a reason' : '' }}.
        </div>
      } @else if (s.status === 'FinalBilled') {
        <div class="banner ok" role="status"><mat-icon>check_circle</mat-icon> The bill is settled. The patient can be discharged.</div>
      } @else if (s.status === 'DischargeOrdered') {
        <div class="banner info" role="status"><mat-icon>info</mat-icon> Discharge ordered by the doctor. Prepare the final bill to fix the discharge time.</div>
      }
      @if (a.releasedWithDue) {
        <div class="banner warn">Discharged with {{ a.releasedWithDue | taka }} due — {{ a.releaseOverrideReason }}</div>
      }

      <div class="layout">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Stay</mat-card-title></mat-card-header>
          <mat-card-content>
            <dl>
              <dt>Admitted</dt><dd>{{ s.admittedAt | date: 'd MMM y, h:mm a' }} · {{ sources[a.source] }}{{ a.sourceReference ? ' (' + a.sourceReference + ')' : '' }}</dd>
              @if (s.dischargedAt) {
                <dt>Discharged</dt><dd>{{ s.dischargedAt | date: 'd MMM y, h:mm a' }}{{ a.releasedAt ? '' : ' (bill cut-off)' }}</dd>
              }
              <dt>Days charged</dt><dd>{{ s.days }}</dd>
              <dt>Doctor</dt><dd>{{ s.doctorName }}{{ a.departmentName ? ' · ' + a.departmentName : '' }}</dd>
              @if (a.currentBedId) {
                <dt>Bed</dt><dd>{{ s.wardName }} {{ s.bedNumber }} · {{ a.currentCategoryName }} · {{ a.currentDailyRate | taka }}/day</dd>
              }
              <dt>Reason</dt><dd>{{ a.reason }}</dd>
              @if (a.emergencyContactName || a.emergencyContactPhone) {
                <dt>Emergency contact</dt>
                <dd>{{ a.emergencyContactName }}{{ a.emergencyContactRelation ? ' (' + a.emergencyContactRelation + ')' : '' }} {{ a.emergencyContactPhone }}</dd>
              }
            </dl>
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined">
          <mat-card-header>
            <mat-card-title>Bill</mat-card-title>
            @if (inBed()) {
              <button mat-icon-button type="button" class="card-action" (click)="syncCharges(a)" aria-label="Update bed charges"><mat-icon>sync</mat-icon></button>
            }
          </mat-card-header>
          <mat-card-content>
            @if (a.bill; as b) {
              <dl>
                <dt>Invoice</dt>
                <dd>
                  @if (can.billing) {
                    <a [routerLink]="['/billing/invoices', b.invoiceId]">{{ b.invoiceNumber }}</a>
                  } @else {
                    {{ b.invoiceNumber }}
                  }
                  · {{ b.status === 'Draft' ? 'running' : b.status.toLowerCase() }}
                </dd>
                <dt>Charges so far</dt><dd>{{ b.netAmount | taka }}</dd>
                @if (b.status === 'Finalized') {
                  <dt>Paid</dt><dd>{{ b.paidAmount | taka }}</dd>
                  <dt class="strong">Due</dt><dd class="strong" [class.due]="b.dueAmount > 0">{{ b.dueAmount | taka }}</dd>
                }
              </dl>
            } @else {
              <p class="secondary">No charges yet.</p>
            }
            <dl class="deposit">
              <dt>Deposit (advance)</dt><dd>{{ a.advanceBalance | taka }}</dd>
              @if (inBed()) {
                <dt>Suggested</dt><dd>{{ a.suggestedDeposit | taka }}</dd>
              }
            </dl>
            @if (inBed() && a.advanceBalance < a.suggestedDeposit) {
              <p class="hint"><mat-icon inline>info</mat-icon> Deposit below the suggested amount. This never blocks treatment.</p>
            }
            @if (can.collect) {
              <a mat-stroked-button [routerLink]="['/billing/patients', s.patientId]"><mat-icon>savings</mat-icon> Patient account</a>
            }
          </mat-card-content>
        </mat-card>
      </div>

      @if (a.discharge; as d) {
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Discharge summary</mat-card-title></mat-card-header>
          <mat-card-content>
            <dl>
              <dt>Type</dt><dd>{{ dischargeTypes[d.type] }}</dd>
              <dt>Final diagnosis</dt><dd>{{ d.finalDiagnosis }}</dd>
              @if (d.hospitalCourse) {
                <dt>Hospital course</dt><dd class="pre">{{ d.hospitalCourse }}</dd>
              }
              @if (d.dischargeMedications) {
                <dt>Medicines</dt><dd class="pre">{{ d.dischargeMedications }}</dd>
              }
              @if (d.advice) {
                <dt>Advice</dt><dd class="pre" lang="bn">{{ d.advice }}</dd>
              }
              @if (d.followUpDate) {
                <dt>Follow-up</dt><dd>{{ d.followUpDate }}</dd>
              }
              <dt>Ordered by</dt><dd>{{ d.orderedByName }}{{ d.orderedAt ? ', ' + (d.orderedAt | date: 'd MMM y, h:mm a') : '' }}</dd>
            </dl>
          </mat-card-content>
        </mat-card>
      } @else if (a.dischargeOrdered) {
        <p class="secondary">The doctor has ordered discharge. The discharge summary is visible to clinical staff.</p>
      }

      <mat-card appearance="outlined">
        <mat-card-header><mat-card-title>Bed history</mat-card-title></mat-card-header>
        <mat-card-content>
          @for (h of a.bedHistory; track h.startedAt) {
            <div class="entry">
              <span class="grow">{{ h.wardName }} bed {{ h.bedNumber }} <span class="secondary">· {{ h.categoryName }} · {{ h.reason }}</span></span>
              <span class="secondary">{{ h.startedAt | date: 'd MMM, h:mm a' }} – {{ h.endedAt ? (h.endedAt | date: 'd MMM, h:mm a') : 'now' }}</span>
            </div>
          }
        </mat-card-content>
      </mat-card>
    } @else {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; }
    .back { margin-left: -12px; }
    h1 { margin: 4px 0; font: var(--mat-sys-headline-small); display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
    .sub { color: var(--mat-sys-on-surface-variant); }
    .sub a { color: inherit; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .warn { --mat-button-filled-container-color: var(--mat-sys-error); --mat-button-filled-label-text-color: var(--mat-sys-on-error); }
    .banner { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-radius: 8px; }
    .banner.error, .banner.warn { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .banner.ok { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .banner.info { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .layout { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr); gap: 12px; align-items: start; }
    mat-card-header { align-items: center; }
    .card-action { margin-left: auto; }
    mat-card-content { padding-top: 8px; }
    dl { display: grid; grid-template-columns: max-content 1fr; gap: 6px 16px; margin: 0 0 8px; }
    dt { color: var(--mat-sys-on-surface-variant); }
    dd { margin: 0; }
    .deposit { padding-top: 8px; border-top: 1px solid var(--mat-sys-outline-variant); }
    .strong { font-weight: 600; }
    .due { color: var(--mat-sys-error); }
    .pre { white-space: pre-wrap; }
    .hint { display: flex; gap: 4px; align-items: center; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .entry { display: flex; flex-wrap: wrap; gap: 4px 12px; padding: 8px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .entry:last-child { border-bottom: 0; }
    .grow { flex: 1; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .badge { padding: 2px 10px; border-radius: 999px; font: var(--mat-sys-label-medium); }
    .badge.Admitted { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .badge.DischargeOrdered, .badge.FinalBilled { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .badge.Discharged { background: var(--mat-sys-surface-container-highest); color: var(--mat-sys-on-surface-variant); }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
  `,
})
export class AdmissionPage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(IpdApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly can = {
    admit: this.auth.hasPermission(IpdPermissions.admit),
    dischargeOrder: this.auth.hasPermission(IpdPermissions.dischargeOrder),
    complete: this.auth.hasPermission(IpdPermissions.dischargeComplete),
    override: this.auth.hasPermission(IpdPermissions.dischargeOverride),
    billing: this.auth.hasPermission(BillingPermissions.view),
    collect: this.auth.hasPermission(BillingPermissions.paymentCollect),
  };
  protected readonly labels = ADMISSION_STATUS_LABELS;
  protected readonly sources = SOURCE_LABELS;
  protected readonly dischargeTypes = DISCHARGE_TYPE_LABELS;
  protected readonly admission = signal<Admission | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly inBed = computed(() => {
    const status = this.admission()?.summary.status;
    return status === 'Admitted' || status === 'DischargeOrdered';
  });
  protected readonly due = computed(() => this.admission()?.bill?.dueAmount ?? 0);

  ngOnInit(): void {
    this.load();
  }

  protected transfer(a: Admission): void {
    this.openAndApply(this.dialog.open(TransferDialog, { data: a, width: '520px' }).afterClosed());
  }

  protected changeDoctor(a: Admission): void {
    this.openAndApply(this.dialog.open(DoctorDialog, { data: a }).afterClosed());
  }

  protected orderDischarge(a: Admission): void {
    this.openAndApply(this.dialog.open(DischargeOrderDialog, { data: a, width: '680px' }).afterClosed());
  }

  protected cancelOrder(a: Admission): void {
    this.confirmThen(
      { title: 'Cancel the discharge order?', message: 'The patient stays admitted. The discharge summary is kept as a draft.', confirmText: 'Cancel order' },
      () => this.api.cancelDischargeOrder(a.summary.id, a.rowVersion),
      'Discharge order cancelled.',
    );
  }

  protected prepareFinalBill(a: Admission): void {
    this.confirmThen(
      {
        title: 'Prepare the final bill?',
        message: 'Now becomes the discharge time: bed charges stop here and the bill is finalized for payment. This cannot be undone.',
        confirmText: 'Prepare final bill',
      },
      () => this.api.prepareFinalBill(a.summary.id, a.rowVersion),
      'Final bill ready for settlement.',
    );
  }

  protected release(a: Admission, overrideReason: string | null): void {
    this.busy.set(true);
    this.api.release(a.summary.id, overrideReason, a.rowVersion).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.admission.set(updated);
        this.notifications.success(`${updated.summary.patientName} discharged. Bed ${a.summary.bedNumber} is marked for cleaning.`);
      },
      error: (err) => {
        this.busy.set(false);
        this.notifications.error(apiErrorMessage(err));
        this.load();
      },
    });
  }

  protected releaseWithDue(a: Admission): void {
    this.dialog
      .open(ReasonDialog, {
        data: {
          title: 'Discharge with amount due',
          message: `${(a.bill?.dueAmount ?? 0).toFixed(2)} stays on the patient's account. Record why the patient is leaving before paying.`,
          label: 'Reason',
          confirmText: 'Discharge with due',
          required: true,
          destructive: true,
        },
      })
      .afterClosed()
      .pipe(filter((text): text is string => !!text))
      .subscribe((reason) => this.release(a, reason));
  }

  protected syncCharges(a: Admission): void {
    this.api.syncCharges(a.summary.id).subscribe({
      next: (updated) => this.admission.set(updated),
      error: (err) => this.notifications.error(apiErrorMessage(err)),
    });
  }

  private openAndApply(closed: Observable<Admission | undefined>): void {
    closed.pipe(filter((a): a is Admission => !!a)).subscribe((a) => this.admission.set(a));
  }

  private confirmThen(data: { title: string; message: string; confirmText: string }, action: () => Observable<Admission>, done: string): void {
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.busy.set(true);
          return action();
        }),
      )
      .subscribe({
        next: (updated) => {
          this.busy.set(false);
          this.admission.set(updated);
          this.notifications.success(done);
        },
        error: (err) => {
          this.busy.set(false);
          this.notifications.error(apiErrorMessage(err));
          this.load();
        },
      });
  }

  private load(): void {
    this.api.admission(this.id()).subscribe({
      next: (a) => this.admission.set(a),
      error: (err) => this.error.set(apiErrorMessage(err, 'The admission could not be loaded.')),
    });
  }
}
