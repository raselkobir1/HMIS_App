import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { filter } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import {
  BillingApiService,
  BillingPermissions,
  Invoice,
  invoiceBadge,
  METHOD_LABELS,
  Receipt,
  Refund,
  REFUND_STATUS_LABELS,
  REFUND_TYPE_LABELS,
  SOURCE_LABELS,
} from '../billing.models';
import { BillingActions } from '../components/billing-actions';
import { DiscountDialog } from '../components/discount-dialog';
import { PaymentDialog } from '../components/payment-dialog';
import { RefundRequestDialog } from '../components/refund-dialogs';
import { TakaPipe } from '../components/taka.pipe';

/** One invoice: lines, totals, discounts, payments and refunds, with the actions the user may take. Route: /billing/invoices/:id. */
@Component({
  selector: 'app-invoice-page',
  imports: [DatePipe, RouterLink, MatCardModule, MatButtonModule, MatIconModule, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <div class="banner error" role="alert">{{ message }}</div>
    } @else if (invoice(); as inv) {
      <header class="head">
        <div>
          <a mat-button [routerLink]="['/billing/patients', inv.patient?.id]" class="back"><mat-icon>arrow_back</mat-icon> Patient account</a>
          <h1>{{ inv.invoiceNumber }} <span [class]="badge().css">{{ badge().label }}</span></h1>
          <div class="sub">
            <a [routerLink]="['/billing/patients', inv.patient?.id]">{{ inv.patient?.fullName }}</a> · {{ inv.patient?.mrNumber }} ·
            {{ inv.invoiceDate }}
            @if (inv.sourceType) {
              · {{ sources[inv.sourceType] ?? inv.sourceType }} {{ inv.sourceReference }}
            }
          </div>
        </div>
        <div class="actions">
          @if (inv.status === 'Draft') {
            @if (can.manage) {
              <a mat-stroked-button [routerLink]="['/billing/invoices', inv.id, 'edit']">
                <mat-icon>{{ inv.isRunningBill ? 'add' : 'edit' }}</mat-icon> {{ inv.isRunningBill ? 'Add services' : 'Edit' }}
              </a>
              @if (!inv.isRunningBill || inv.isSupplementary) {
                <button mat-flat-button type="button" [disabled]="busy()" (click)="finalize(inv)"><mat-icon>lock</mat-icon> Finalize</button>
                <button mat-button type="button" (click)="cancel(inv)">Discard</button>
              }
            }
          } @else {
            @if (payable()) {
              @if (can.collect) {
                <button mat-flat-button type="button" (click)="collect(inv)"><mat-icon>payments</mat-icon> Receive payment</button>
              }
              @if (can.discount && !pendingDiscount()) {
                <button mat-stroked-button type="button" (click)="discount(inv)"><mat-icon>percent</mat-icon> Discount</button>
              }
            }
            <a mat-stroked-button [routerLink]="['/billing/invoices', inv.id, 'print']"><mat-icon>print</mat-icon> Print</a>
            @if (inv.status === 'Finalized' && !inv.cancellationRequested) {
              @if (cashHeld() > 0 && can.refund) {
                <button mat-button type="button" (click)="cancelAndRefund(inv)">Cancel & refund</button>
              } @else if (cashHeld() <= 0 && can.cancel) {
                <button mat-button type="button" (click)="cancel(inv)">Cancel invoice</button>
              }
            }
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
      @if (inv.isRunningBill && inv.isSupplementary) {
        <div class="banner warn" role="status">
          <mat-icon>post_add</mat-icon>
          Supplementary bill: these charges arrived after the final bill of this stay. Finalize it and collect the amount due.
        </div>
      } @else if (inv.isRunningBill) {
        <div class="banner info" role="status">
          <mat-icon>hotel</mat-icon>
          Running bill for an IPD stay. Bed rent is added daily; the bill is finalized from the ward at discharge. Deposits are taken as advance.
        </div>
      }
      @if (inv.cancellationRequested) {
        <div class="banner warn" role="status">A cancellation refund is in progress. Payments and discounts are paused until it is paid out or rejected.</div>
      }
      @if (inv.status === 'Cancelled') {
        <div class="banner muted-banner">Cancelled {{ inv.cancelledAt | date: 'd MMM y, h:mm a' }} — {{ inv.cancellationReason }}</div>
      }

      <div class="layout">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Charges</mat-card-title></mat-card-header>
          <mat-card-content>
            <table class="grid">
              <thead>
                <tr><th>Service</th><th class="num">Qty</th><th class="num">Unit price</th><th class="num">Total</th></tr>
              </thead>
              <tbody>
                @for (l of inv.lines; track l.id) {
                  <tr>
                    <td>{{ l.description }}<div class="secondary">{{ l.serviceCode ? l.serviceCode + ' · ' : '' }}{{ l.category }}</div></td>
                    <td class="num">{{ l.quantity }}</td>
                    <td class="num">{{ l.unitPrice | taka }}</td>
                    <td class="num">{{ l.lineTotal | taka }}</td>
                  </tr>
                }
              </tbody>
            </table>
            @if (inv.notes) {
              <p class="secondary">Notes: {{ inv.notes }}</p>
            }
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined" class="totals">
          <mat-card-header><mat-card-title>Amount</mat-card-title></mat-card-header>
          <mat-card-content>
            <dl>
              <dt>Subtotal</dt><dd>{{ inv.subtotal | taka }}</dd>
              @if (inv.discountAmount > 0) {
                <dt>Discount</dt><dd>− {{ inv.discountAmount | taka }}</dd>
              }
              <dt class="strong">Net</dt><dd class="strong">{{ inv.netAmount | taka }}</dd>
              <dt>Paid</dt><dd>{{ inv.paidAmount | taka }}</dd>
              @if (inv.advanceAppliedAmount > 0) {
                <dt class="indent">of which from advance</dt><dd>{{ inv.advanceAppliedAmount | taka }}</dd>
              }
              @if (inv.refundedAmount > 0) {
                <dt>Refunded</dt><dd>{{ inv.refundedAmount | taka }}</dd>
              }
              @if (inv.status === 'Finalized') {
                <dt class="strong">Due</dt><dd class="strong" [class.due]="inv.dueAmount > 0">{{ inv.dueAmount | taka }}</dd>
              }
            </dl>
            @if (inv.patientAdvanceBalance > 0) {
              <p class="secondary">Patient's advance balance: {{ inv.patientAdvanceBalance | taka }}</p>
            }
          </mat-card-content>
        </mat-card>
      </div>

      @if (inv.discounts.length) {
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Discounts</mat-card-title></mat-card-header>
          <mat-card-content>
            @for (d of inv.discounts; track d.id) {
              <div class="entry">
                <div class="grow">
                  <strong>{{ d.amount | taka }}</strong>{{ d.type === 'Percent' ? ' (' + d.value + '%)' : '' }} — {{ d.reason }}
                  <div class="secondary">
                    Requested by {{ d.requestedByName }}, {{ d.requestedAt | date: 'd MMM, h:mm a' }}
                    @if (d.decidedAt) {
                      · {{ d.status === 'Approved' ? 'approved' : 'rejected' }} by {{ d.decidedByName ?? 'system' }}{{ d.decisionNote ? ': ' + d.decisionNote : '' }}
                    }
                  </div>
                </div>
                @if (d.status === 'Pending') {
                  @if (can.approveDiscount) {
                    <button mat-flat-button type="button" (click)="decideDiscount(d.id, true)">Approve</button>
                    <button mat-button type="button" (click)="decideDiscount(d.id, false)">Reject</button>
                  } @else {
                    <span class="badge warn">Awaiting approval</span>
                  }
                } @else {
                  <span class="badge" [class.ok]="d.status === 'Approved'" [class.muted]="d.status === 'Rejected'">{{ d.status }}</span>
                }
              </div>
            }
          </mat-card-content>
        </mat-card>
      }

      @if (inv.payments.length) {
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Payments</mat-card-title></mat-card-header>
          <mat-card-content>
            @for (p of inv.payments; track p.id) {
              <div class="entry">
                <div class="grow">
                  <strong>{{ p.amount | taka }}</strong> · {{ methods[p.method] }}{{ p.reference ? ' (' + p.reference + ')' : '' }}
                  <div class="secondary">{{ p.receiptNumber }} · {{ p.receivedAt | date: 'd MMM y, h:mm a' }} · {{ p.receivedByName }}</div>
                </div>
                <a mat-icon-button [routerLink]="['/billing/receipts', p.id, 'print']" [attr.aria-label]="'Print receipt ' + p.receiptNumber">
                  <mat-icon>print</mat-icon>
                </a>
              </div>
            }
          </mat-card-content>
        </mat-card>
      }

      @if (inv.refunds.length) {
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Refunds</mat-card-title></mat-card-header>
          <mat-card-content>
            @for (r of inv.refunds; track r.id) {
              <div class="entry">
                <div class="grow">
                  <strong>{{ r.amount | taka }}</strong> · {{ refundTypes[r.type] }} — {{ r.reason }}
                  <div class="secondary">
                    {{ r.refundNumber }} · requested by {{ r.requestedByName }}
                    @if (r.paidAt) {
                      · paid {{ r.paidAt | date: 'd MMM, h:mm a' }} by {{ r.paidByName }} ({{ methods[r.method!] }})
                    }
                    @if (r.decisionNote) {
                      · {{ r.decisionNote }}
                    }
                  </div>
                </div>
                @if (r.status === 'Requested' && can.approveRefund) {
                  <button mat-flat-button type="button" (click)="decideRefund(r, true)">Approve</button>
                  <button mat-button type="button" (click)="decideRefund(r, false)">Reject</button>
                } @else if (r.status === 'Approved' && can.refund) {
                  <button mat-flat-button type="button" (click)="payRefund(r)">Pay out</button>
                } @else {
                  <span class="badge" [class.ok]="r.status === 'Paid'" [class.warn]="r.status === 'Requested' || r.status === 'Approved'"
                        [class.muted]="r.status === 'Rejected'">{{ refundStatuses[r.status] }}</span>
                }
              </div>
            }
          </mat-card-content>
        </mat-card>
      }

      <p class="secondary footer">Created {{ inv.createdAt | date: 'd MMM y, h:mm a' }}{{ inv.createdByName ? ' by ' + inv.createdByName : '' }}</p>
    } @else {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; }
    .back { margin-left: -12px; }
    h1 { margin: 4px 0; font: var(--mat-sys-headline-small); display: flex; align-items: center; gap: 12px; }
    .sub { color: var(--mat-sys-on-surface-variant); }
    .sub a { color: inherit; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .banner { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-radius: 8px; }
    .banner.error, .banner.warn { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .banner.ok { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .banner.info { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .banner.muted-banner { background: var(--mat-sys-surface-container); color: var(--mat-sys-on-surface-variant); }
    .layout { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 12px; align-items: start; }
    mat-card-content { padding-top: 8px; }
    .grid { width: 100%; border-collapse: collapse; }
    .grid th { text-align: left; font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
    .grid th, .grid td { padding: 6px 4px; border-bottom: 1px solid var(--mat-sys-outline-variant); vertical-align: top; }
    .num { text-align: right !important; white-space: nowrap; }
    dl { display: grid; grid-template-columns: 1fr auto; gap: 6px 12px; margin: 0; }
    dd { margin: 0; text-align: right; }
    .strong { font-weight: 600; }
    .indent { padding-left: 12px; color: var(--mat-sys-on-surface-variant); }
    .due { color: var(--mat-sys-error); }
    .entry { display: flex; align-items: center; gap: 8px; padding: 8px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .entry:last-child { border-bottom: 0; }
    .grow { flex: 1; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .footer { margin: 0; }
    .badge { display: inline-block; padding: 2px 10px; border-radius: 999px; font: var(--mat-sys-label-medium); white-space: nowrap; }
    .badge.ok { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .badge.warn { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .badge.info { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .badge.muted { background: var(--mat-sys-surface-container-highest); color: var(--mat-sys-on-surface-variant); }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
  `,
})
export class InvoicePage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(BillingApiService);
  private readonly actions = inject(BillingActions);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly can = {
    manage: this.auth.hasPermission(BillingPermissions.invoiceManage),
    cancel: this.auth.hasPermission(BillingPermissions.invoiceCancel),
    collect: this.auth.hasPermission(BillingPermissions.paymentCollect),
    discount: this.auth.hasPermission(BillingPermissions.discountApply),
    approveDiscount: this.auth.hasPermission(BillingPermissions.discountApprove),
    refund: this.auth.hasPermission(BillingPermissions.refundRequest),
    approveRefund: this.auth.hasPermission(BillingPermissions.refundApprove),
  };
  protected readonly methods = METHOD_LABELS;
  protected readonly sources = SOURCE_LABELS;
  protected readonly refundTypes = REFUND_TYPE_LABELS;
  protected readonly refundStatuses = REFUND_STATUS_LABELS;

  protected readonly invoice = signal<Invoice | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly lastReceipt = signal<Receipt | null>(null);

  protected readonly badge = computed(() => invoiceBadge(this.invoice()!));
  protected readonly cashHeld = computed(() => {
    const i = this.invoice();
    return i ? i.paidAmount - i.advanceAppliedAmount - i.refundedAmount : 0;
  });
  protected readonly payable = computed(() => {
    const i = this.invoice();
    return !!i && i.status === 'Finalized' && !i.cancellationRequested && i.dueAmount > 0;
  });
  protected readonly pendingDiscount = computed(() => this.invoice()?.discounts.some((d) => d.status === 'Pending') ?? false);

  ngOnInit(): void {
    this.load();
  }

  protected finalize(inv: Invoice): void {
    this.busy.set(true);
    this.api.finalizeInvoice(inv.id, inv.rowVersion).subscribe({
      next: (i) => {
        this.busy.set(false);
        this.invoice.set(i);
        this.notifications.success(`Invoice ${i.invoiceNumber} finalized.`);
      },
      error: (err) => {
        this.busy.set(false);
        this.notifications.error(apiErrorMessage(err));
        this.load();
      },
    });
  }

  protected collect(inv: Invoice): void {
    this.dialog
      .open(PaymentDialog, {
        width: '560px',
        data: {
          patientId: inv.patient!.id,
          patientLabel: `${inv.patient!.fullName} · ${inv.patient!.mrNumber}`,
          invoice: { id: inv.id, number: inv.invoiceNumber, due: inv.dueAmount },
          advanceAvailable: inv.patientAdvanceBalance,
        },
      })
      .afterClosed()
      .pipe(filter((r): r is Receipt => !!r))
      .subscribe((receipt) => {
        this.lastReceipt.set(receipt);
        this.load();
      });
  }

  protected discount(inv: Invoice): void {
    this.dialog
      .open(DiscountDialog, { width: '560px', data: { invoice: inv, canApprove: this.can.approveDiscount } })
      .afterClosed()
      .pipe(filter((i): i is Invoice => !!i))
      .subscribe(() => this.load());
  }

  protected cancel(inv: Invoice): void {
    this.actions.cancelInvoice(inv).subscribe((i) => this.invoice.set(i));
  }

  protected cancelAndRefund(inv: Invoice): void {
    this.dialog
      .open(RefundRequestDialog, {
        width: '560px',
        data: {
          patientId: inv.patient!.id,
          patientLabel: `${inv.patient!.fullName} · ${inv.patient!.mrNumber}`,
          type: 'InvoiceCancellation',
          invoice: { id: inv.id, number: inv.invoiceNumber, cashHeld: this.cashHeld() },
          advanceAvailable: inv.patientAdvanceBalance,
          canApprove: this.can.approveRefund,
        },
      })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }

  protected decideDiscount(discountId: string, approve: boolean): void {
    this.actions.decideDiscount(discountId, approve).subscribe((i) => this.invoice.set(i));
  }

  protected decideRefund(refund: Refund, approve: boolean): void {
    this.actions.decideRefund(refund, approve).subscribe(() => this.load());
  }

  protected payRefund(refund: Refund): void {
    this.actions.payRefund(refund).subscribe(() => this.load());
  }

  private load(): void {
    this.api.invoice(this.id()).subscribe({
      next: (i) => this.invoice.set(i),
      error: (err) => this.error.set(apiErrorMessage(err, 'The invoice could not be loaded.')),
    });
  }
}
