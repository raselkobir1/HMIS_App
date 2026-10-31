import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { filter } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import {
  BillingApiService,
  BillingPermissions,
  invoiceBadge,
  METHOD_LABELS,
  PatientAccount,
  Receipt,
  Refund,
  REFUND_STATUS_LABELS,
  REFUND_TYPE_LABELS,
  SOURCE_LABELS,
} from '../billing.models';
import { BillingActions } from '../components/billing-actions';
import { PaymentDialog } from '../components/payment-dialog';
import { RefundRequestDialog } from '../components/refund-dialogs';
import { TakaPipe } from '../components/taka.pipe';

/** A patient's billing account: amount due, advance, invoices, payments and refunds. Route: /billing/patients/:patientId. */
@Component({
  selector: 'app-patient-account-page',
  imports: [DatePipe, RouterLink, MatCardModule, MatButtonModule, MatIconModule, MatTabsModule, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <div class="banner error" role="alert">{{ message }}</div>
    } @else if (account(); as a) {
      <header class="head">
        <div>
          <h1>{{ a.patient.fullName }}</h1>
          <div class="sub">
            <a [routerLink]="['/patients', a.patient.id]">{{ a.patient.mrNumber }}</a> · {{ a.patient.mobileNumber }}
            @if (a.patient.status !== 'Active') {
              · <span class="badge muted">{{ a.patient.status }}</span>
            }
          </div>
        </div>
        <div class="actions">
          @if (can.manage && a.patient.status === 'Active') {
            <a mat-flat-button routerLink="/billing/invoices/new" [queryParams]="{ patientId: a.patient.id }"><mat-icon>add</mat-icon> New invoice</a>
          }
          @if (can.collect && a.patient.status === 'Active') {
            <button mat-stroked-button type="button" (click)="deposit()"><mat-icon>savings</mat-icon> Advance deposit</button>
          }
          @if (can.refund && a.advanceBalance > 0) {
            <button mat-button type="button" (click)="returnAdvance()">Return advance</button>
          }
        </div>
      </header>

      @if (lastReceipt(); as r) {
        <div class="banner ok" role="status">
          <mat-icon>check_circle</mat-icon>
          Received {{ r.payment.amount | taka }} — receipt {{ r.payment.receiptNumber }}
          <a mat-button [routerLink]="['/billing/receipts', r.payment.id, 'print']"><mat-icon>print</mat-icon> Print receipt</a>
        </div>
      }

      <div class="cards">
        <mat-card appearance="outlined" class="figure" [class.alert]="a.outstandingAmount > 0">
          <mat-card-content>
            <div class="label">Amount due</div>
            <div class="value">{{ a.outstandingAmount | taka }}</div>
          </mat-card-content>
        </mat-card>
        <mat-card appearance="outlined" class="figure">
          <mat-card-content>
            <div class="label">Advance balance</div>
            <div class="value">{{ a.advanceBalance | taka }}</div>
          </mat-card-content>
        </mat-card>
      </div>

      <mat-tab-group animationDuration="0ms">
        <mat-tab [label]="'Invoices (' + a.invoices.length + ')'">
          <div class="list">
            @for (i of a.invoices; track i.id) {
              @let badge = badgeOf(i);
              <a class="row link" [routerLink]="['/billing/invoices', i.id]">
                <span class="grow">
                  <strong>{{ i.invoiceNumber }}</strong> · {{ i.invoiceDate }}
                  <span class="secondary">{{ i.sourceType ? (sources[i.sourceType] ?? i.sourceType) + ' ' + (i.sourceReference ?? '') : 'Manual invoice' }}</span>
                </span>
                <span class="num">{{ i.netAmount | taka }}@if (i.dueAmount > 0) {<span class="secondary due">due {{ i.dueAmount | taka }}</span>}</span>
                <span [class]="badge.css">{{ badge.label }}</span>
              </a>
            } @empty {
              <p class="secondary empty">No invoices yet.</p>
            }
          </div>
        </mat-tab>
        <mat-tab [label]="'Payments (' + a.payments.length + ')'">
          <div class="list">
            @for (p of a.payments; track p.id) {
              <div class="row">
                <span class="grow">
                  <strong>{{ p.amount | taka }}</strong> · {{ methods[p.method] }}{{ p.reference ? ' (' + p.reference + ')' : '' }}
                  <span class="secondary">
                    {{ p.receiptNumber }} · {{ p.invoiceNumber ?? 'Advance deposit' }} · {{ p.receivedAt | date: 'd MMM y, h:mm a' }} · {{ p.receivedByName }}
                  </span>
                </span>
                <a mat-icon-button [routerLink]="['/billing/receipts', p.id, 'print']" [attr.aria-label]="'Print receipt ' + p.receiptNumber">
                  <mat-icon>print</mat-icon>
                </a>
              </div>
            } @empty {
              <p class="secondary empty">No payments yet.</p>
            }
          </div>
        </mat-tab>
        <mat-tab [label]="'Refunds (' + a.refunds.length + ')'">
          <div class="list">
            @for (r of a.refunds; track r.id) {
              <div class="row">
                <span class="grow">
                  <strong>{{ r.amount | taka }}</strong> · {{ refundTypes[r.type] }}{{ r.invoiceNumber ? ' ' + r.invoiceNumber : '' }} — {{ r.reason }}
                  <span class="secondary">{{ r.refundNumber }} · {{ r.requestedAt | date: 'd MMM y' }} · {{ r.requestedByName }}</span>
                </span>
                @if (r.status === 'Requested' && can.approveRefund) {
                  <button mat-flat-button type="button" (click)="decide(r, true)">Approve</button>
                  <button mat-button type="button" (click)="decide(r, false)">Reject</button>
                } @else if (r.status === 'Approved' && can.refund) {
                  <button mat-flat-button type="button" (click)="pay(r)">Pay out</button>
                } @else {
                  <span class="badge" [class.ok]="r.status === 'Paid'" [class.warn]="r.status === 'Requested' || r.status === 'Approved'"
                        [class.muted]="r.status === 'Rejected'">{{ refundStatuses[r.status] }}</span>
                }
              </div>
            } @empty {
              <p class="secondary empty">No refunds.</p>
            }
          </div>
        </mat-tab>
      </mat-tab-group>
    } @else {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; }
    h1 { margin: 0; font: var(--mat-sys-headline-small); }
    .sub { color: var(--mat-sys-on-surface-variant); }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .banner { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-radius: 8px; }
    .banner.error { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .banner.ok { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 260px)); gap: 12px; }
    .figure .label { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-large); }
    .figure .value { font: var(--mat-sys-headline-small); }
    .figure.alert .value { color: var(--mat-sys-error); }
    .list { display: flex; flex-direction: column; }
    .row { display: flex; align-items: center; gap: 12px; padding: 10px 4px; border-bottom: 1px solid var(--mat-sys-outline-variant); color: inherit; text-decoration: none; }
    .row.link:hover { background: var(--mat-sys-surface-container); }
    .grow { flex: 1; min-width: 0; }
    .num { text-align: right; white-space: nowrap; }
    .secondary { display: block; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .due { color: var(--mat-sys-error); }
    .empty { padding: 16px 4px; }
    .badge { display: inline-block; padding: 2px 10px; border-radius: 999px; font: var(--mat-sys-label-medium); white-space: nowrap; }
    .badge.ok { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .badge.warn { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .badge.info { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .badge.muted { background: var(--mat-sys-surface-container-highest); color: var(--mat-sys-on-surface-variant); }
  `,
})
export class PatientAccountPage implements OnInit {
  readonly patientId = input.required<string>();

  private readonly api = inject(BillingApiService);
  private readonly actions = inject(BillingActions);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  protected readonly can = {
    manage: this.auth.hasPermission(BillingPermissions.invoiceManage),
    collect: this.auth.hasPermission(BillingPermissions.paymentCollect),
    refund: this.auth.hasPermission(BillingPermissions.refundRequest),
    approveRefund: this.auth.hasPermission(BillingPermissions.refundApprove),
  };
  protected readonly methods = METHOD_LABELS;
  protected readonly sources = SOURCE_LABELS;
  protected readonly refundTypes = REFUND_TYPE_LABELS;
  protected readonly refundStatuses = REFUND_STATUS_LABELS;
  protected readonly badgeOf = invoiceBadge;

  protected readonly account = signal<PatientAccount | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly lastReceipt = signal<Receipt | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected deposit(): void {
    const a = this.account()!;
    this.dialog
      .open(PaymentDialog, {
        width: '560px',
        data: { patientId: a.patient.id, patientLabel: this.label(a), invoice: null, advanceAvailable: a.advanceBalance },
      })
      .afterClosed()
      .pipe(filter((r): r is Receipt => !!r))
      .subscribe((receipt) => {
        this.lastReceipt.set(receipt);
        this.load();
      });
  }

  protected returnAdvance(): void {
    const a = this.account()!;
    this.dialog
      .open(RefundRequestDialog, {
        width: '560px',
        data: {
          patientId: a.patient.id,
          patientLabel: this.label(a),
          type: 'AdvanceReturn',
          advanceAvailable: a.advanceBalance,
          canApprove: this.can.approveRefund,
        },
      })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }

  protected decide(refund: Refund, approve: boolean): void {
    this.actions.decideRefund(refund, approve).subscribe(() => this.load());
  }

  protected pay(refund: Refund): void {
    this.actions.payRefund(refund).subscribe(() => this.load());
  }

  private label(a: PatientAccount): string {
    return `${a.patient.fullName} · ${a.patient.mrNumber}`;
  }

  private load(): void {
    this.api.account(this.patientId()).subscribe({
      next: (a) => this.account.set(a),
      error: (err) => this.error.set(apiErrorMessage(err, 'The account could not be loaded.')),
    });
  }
}
