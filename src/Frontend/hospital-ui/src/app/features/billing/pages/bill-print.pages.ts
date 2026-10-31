import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { BillingApiService, Invoice, METHOD_LABELS, Receipt, SOURCE_LABELS } from '../billing.models';
import { BillLetterhead } from '../components/bill-letterhead';
import { TakaPipe } from '../components/taka.pipe';

/** Shared A5 page styles for printed bills. The app shell is hidden when printing (global print styles). */
const PRINT_STYLES = `
  .toolbar { display: flex; gap: 8px; margin-bottom: 16px; }
  .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
  .page {
    box-sizing: border-box; width: 148mm; min-height: 210mm; margin: 0 auto; padding: 10mm;
    display: flex; flex-direction: column; gap: 6px; background: #fff; color: #111; font: 12px/1.4 Roboto, Arial, sans-serif;
    box-shadow: 0 1px 6px rgb(0 0 0 / 0.2);
  }
  .meta { display: grid; grid-template-columns: 1fr 1fr; gap: 2px 12px; padding: 4px 0 6px; border-bottom: 1px solid #999; font-size: 11px; }
  .meta .right { text-align: right; }
  table { width: 100%; border-collapse: collapse; font-size: 11px; }
  th { text-align: left; border-bottom: 1px solid #111; padding: 3px 2px; }
  td { padding: 3px 2px; border-bottom: 1px solid #ddd; vertical-align: top; }
  .num { text-align: right; white-space: nowrap; }
  .totals { margin-left: auto; min-width: 60mm; font-size: 12px; }
  .totals div { display: flex; justify-content: space-between; gap: 12px; padding: 1px 0; }
  .totals .strong { font-weight: 600; border-top: 1px solid #111; padding-top: 3px; }
  .stamp { align-self: flex-start; padding: 2px 10px; border: 2px solid currentColor; border-radius: 4px; font-weight: 700; letter-spacing: 0.08em; }
  .stamp.paid { color: #1b6e2d; }
  .stamp.due { color: #a11; }
  .stamp.cancelled { color: #666; }
  .amount { font-size: 18px; font-weight: 600; }
  footer { margin-top: auto; display: flex; justify-content: space-between; align-items: flex-end; font-size: 10px; color: #444; padding-top: 14mm; }
  .signature { text-align: center; min-width: 40mm; color: #111; }
  .line { border-top: 1px solid #111; margin-bottom: 2px; }
  @media print {
    .page { width: auto; min-height: auto; margin: 0; padding: 0; box-shadow: none; }
    @page { size: A5; margin: 8mm; }
  }
`;

/** A5 invoice with lines, totals and payments. Route: /billing/invoices/:id/print. */
@Component({
  selector: 'app-invoice-print-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, BillLetterhead, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toolbar no-print">
      <a mat-button [routerLink]="['/billing/invoices', id()]"><mat-icon>arrow_back</mat-icon> Back</a>
      <button mat-flat-button type="button" (click)="print()" [disabled]="!invoice() || invoice()!.status === 'Draft'"><mat-icon>print</mat-icon> Print</button>
    </div>
    @if (error(); as message) {
      <div class="banner no-print" role="alert">{{ message }}</div>
    } @else if (invoice(); as inv) {
      @if (inv.status === 'Draft') {
        <div class="banner no-print" role="alert">Finalize the invoice before printing it.</div>
      } @else {
        <article class="page print-area">
          <app-bill-letterhead title="Invoice" />
          <section class="meta">
            <span><strong>{{ inv.patient?.fullName }}</strong></span>
            <span class="right"><strong>{{ inv.invoiceNumber }}</strong></span>
            <span>MR No: {{ inv.patient?.mrNumber }} · {{ inv.patient?.mobileNumber }}</span>
            <span class="right">Date: {{ inv.invoiceDate }}</span>
            @if (inv.sourceType) {
              <span>{{ sources[inv.sourceType] ?? inv.sourceType }}: {{ inv.sourceReference }}</span>
            }
          </section>
          <table>
            <thead><tr><th>#</th><th>Service</th><th class="num">Qty</th><th class="num">Rate</th><th class="num">Amount</th></tr></thead>
            <tbody>
              @for (l of inv.lines; track l.id; let i = $index) {
                <tr><td>{{ i + 1 }}</td><td>{{ l.description }}</td><td class="num">{{ l.quantity }}</td><td class="num">{{ l.unitPrice | taka }}</td><td class="num">{{ l.lineTotal | taka }}</td></tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div><span>Subtotal</span><span>{{ inv.subtotal | taka }}</span></div>
            @if (inv.discountAmount > 0) {
              <div><span>Discount</span><span>− {{ inv.discountAmount | taka }}</span></div>
            }
            <div class="strong"><span>Net payable</span><span>{{ inv.netAmount | taka }}</span></div>
            <div><span>Paid</span><span>{{ inv.paidAmount | taka }}</span></div>
            @if (inv.refundedAmount > 0) {
              <div><span>Refunded</span><span>{{ inv.refundedAmount | taka }}</span></div>
            }
            @if (inv.status === 'Finalized') {
              <div class="strong"><span>Due</span><span>{{ inv.dueAmount | taka }}</span></div>
            }
          </div>
          @if (inv.status === 'Cancelled') {
            <div class="stamp cancelled">CANCELLED</div>
          } @else if (inv.dueAmount <= 0) {
            <div class="stamp paid">PAID</div>
          } @else {
            <div class="stamp due">DUE {{ inv.dueAmount | taka }}</div>
          }
          @if (inv.payments.length) {
            <table>
              <thead><tr><th>Receipt</th><th>Date</th><th>Method</th><th class="num">Amount</th></tr></thead>
              <tbody>
                @for (p of inv.payments; track p.id) {
                  <tr><td>{{ p.receiptNumber }}</td><td>{{ p.receivedAt | date: 'd MMM y, h:mm a' }}</td><td>{{ methods[p.method] }}</td><td class="num">{{ p.amount | taka }}</td></tr>
                }
              </tbody>
            </table>
          }
          <footer>
            <div>Printed {{ now | date: 'd MMM y, h:mm a' }}</div>
            <div class="signature"><div class="line"></div><div>Authorised signature</div></div>
          </footer>
        </article>
      }
    }
  `,
  styles: PRINT_STYLES,
})
export class InvoicePrintPage implements OnInit {
  readonly id = input.required<string>();
  private readonly api = inject(BillingApiService);

  protected readonly methods = METHOD_LABELS;
  protected readonly sources = SOURCE_LABELS;
  protected readonly now = new Date();
  protected readonly invoice = signal<Invoice | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.invoice(this.id()).subscribe({
      next: (i) => this.invoice.set(i),
      error: (err) => this.error.set(apiErrorMessage(err, 'The invoice could not be loaded.')),
    });
  }

  protected print(): void {
    window.print();
  }
}

/** A5 money receipt for one payment or advance deposit. Route: /billing/receipts/:id/print. */
@Component({
  selector: 'app-receipt-print-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, BillLetterhead, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toolbar no-print">
      @if (receipt(); as r) {
        @if (r.invoice) {
          <a mat-button [routerLink]="['/billing/invoices', r.invoice.id]"><mat-icon>arrow_back</mat-icon> Back</a>
        } @else {
          <a mat-button [routerLink]="['/billing/patients', r.payment.patientId]"><mat-icon>arrow_back</mat-icon> Back</a>
        }
      }
      <button mat-flat-button type="button" (click)="print()" [disabled]="!receipt()"><mat-icon>print</mat-icon> Print</button>
    </div>
    @if (error(); as message) {
      <div class="banner no-print" role="alert">{{ message }}</div>
    } @else if (receipt(); as r) {
      <article class="page print-area">
        <app-bill-letterhead [title]="r.invoice ? 'Money receipt' : 'Advance receipt'" />
        <section class="meta">
          <span><strong>{{ r.patient?.fullName }}</strong></span>
          <span class="right"><strong>{{ r.payment.receiptNumber }}</strong></span>
          <span>MR No: {{ r.patient?.mrNumber }} · {{ r.patient?.mobileNumber }}</span>
          <span class="right">{{ r.payment.receivedAt | date: 'd MMM y, h:mm a' }}</span>
        </section>

        <p>
          Received <span class="amount">{{ r.payment.amount | taka }}</span>
          {{ r.payment.method === 'Advance' ? "from the patient's advance" : 'by ' + methods[r.payment.method] }}{{ r.payment.reference ? ' (ref. ' + r.payment.reference + ')' : '' }}
          {{ r.invoice ? 'against invoice ' + r.invoice.invoiceNumber : 'as an advance deposit' }}.
        </p>
        @if (r.payment.notes) {
          <p>Note: {{ r.payment.notes }}</p>
        }

        @if (r.invoice; as inv) {
          <table>
            <thead><tr><th>Service</th><th class="num">Qty</th><th class="num">Amount</th></tr></thead>
            <tbody>
              @for (l of r.lines; track l.id) {
                <tr><td>{{ l.description }}</td><td class="num">{{ l.quantity }}</td><td class="num">{{ l.lineTotal | taka }}</td></tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div><span>Invoice total</span><span>{{ inv.subtotal | taka }}</span></div>
            @if (inv.discountAmount > 0) {
              <div><span>Discount</span><span>− {{ inv.discountAmount | taka }}</span></div>
            }
            <div class="strong"><span>Net payable</span><span>{{ inv.netAmount | taka }}</span></div>
            <div><span>Paid to date</span><span>{{ inv.paidAmount | taka }}</span></div>
            <div class="strong"><span>Balance due</span><span>{{ inv.dueAmount | taka }}</span></div>
          </div>
        } @else {
          <div class="totals">
            <div class="strong"><span>Advance balance</span><span>{{ r.patientAdvanceBalance | taka }}</span></div>
          </div>
        }

        <footer>
          <div>Received by {{ r.payment.receivedByName }}<br />Printed {{ now | date: 'd MMM y, h:mm a' }}</div>
          <div class="signature"><div class="line"></div><div>Cashier's signature</div></div>
        </footer>
      </article>
    }
  `,
  styles: PRINT_STYLES,
})
export class ReceiptPrintPage implements OnInit {
  readonly id = input.required<string>();
  private readonly api = inject(BillingApiService);

  protected readonly methods = METHOD_LABELS;
  protected readonly now = new Date();
  protected readonly receipt = signal<Receipt | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.receipt(this.id()).subscribe({
      next: (r) => this.receipt.set(r),
      error: (err) => this.error.set(apiErrorMessage(err, 'The receipt could not be loaded.')),
    });
  }

  protected print(): void {
    window.print();
  }
}
