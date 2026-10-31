import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';

import { PageHeader } from '../../../shared/components/page-header/page-header';
import { toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { BillingApiService, CollectionReport, METHOD_LABELS } from '../billing.models';
import { TakaPipe } from '../components/taka.pipe';

const MAX_DAYS = 31;

/**
 * Money received and refunded by cashier and payment method. Users without Billing.Reports.View see their own
 * collection only (the server enforces this). Route: /billing/collections.
 */
@Component({
  selector: 'app-collections-page',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    PageHeader,
    TakaPipe,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Collections" [subtitle]="subtitle()">
      <button mat-stroked-button type="button" class="no-print" (click)="print()"><mat-icon>print</mat-icon> Print</button>
    </app-page-header>

    <section class="filters no-print">
      <mat-form-field>
        <mat-label>Dates</mat-label>
        <mat-date-range-input [rangePicker]="range">
          <input matStartDate placeholder="From" [(ngModel)]="from" />
          <input matEndDate placeholder="To" [(ngModel)]="to" />
        </mat-date-range-input>
        <mat-datepicker-toggle matIconSuffix [for]="range" />
        <mat-date-range-picker #range />
        <mat-hint>Up to {{ maxDays }} days</mat-hint>
      </mat-form-field>
      @if (report()?.allCashiers) {
        <mat-form-field>
          <mat-label>Cashier</mat-label>
          <mat-select [(ngModel)]="cashierId">
            <mat-option [value]="null">All cashiers</mat-option>
            @for (c of cashiers(); track c.userId) {
              <mat-option [value]="c.userId">{{ c.name ?? 'Unknown' }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      }
      <button mat-flat-button type="button" (click)="load()">Show</button>
    </section>

    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    } @else if (report(); as r) {
      <div class="cards">
        <mat-card appearance="outlined"><mat-card-content>
          <div class="label">Received</div><div class="value">{{ r.totalReceived | taka }}</div>
        </mat-card-content></mat-card>
        <mat-card appearance="outlined"><mat-card-content>
          <div class="label">Refunded</div><div class="value">{{ r.totalRefunded | taka }}</div>
        </mat-card-content></mat-card>
        <mat-card appearance="outlined" class="net"><mat-card-content>
          <div class="label">Net collection</div><div class="value">{{ r.net | taka }}</div>
        </mat-card-content></mat-card>
      </div>
      @if (r.advanceApplied > 0) {
        <p class="secondary">Advance applied to invoices: {{ r.advanceApplied | taka }} (counted when the deposit was received, not again here).</p>
      }

      <div class="two">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>By method</mat-card-title></mat-card-header>
          <mat-card-content>
            <table class="grid">
              <thead><tr><th>Method</th><th class="num">Received</th><th class="num">Refunded</th><th class="num">Net</th></tr></thead>
              <tbody>
                @for (m of r.byMethod; track m.method) {
                  <tr><td>{{ methods[m.method] }}</td><td class="num">{{ m.received | taka }}</td><td class="num">{{ m.refunded | taka }}</td><td class="num">{{ m.net | taka }}</td></tr>
                } @empty {
                  <tr><td colspan="4" class="secondary">No transactions.</td></tr>
                }
              </tbody>
            </table>
          </mat-card-content>
        </mat-card>
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>By cashier</mat-card-title></mat-card-header>
          <mat-card-content>
            <table class="grid">
              <thead><tr><th>Cashier</th><th class="num">Received</th><th class="num">Refunded</th><th class="num">Net</th></tr></thead>
              <tbody>
                @for (c of r.byCashier; track c.userId) {
                  <tr><td>{{ c.name ?? 'Unknown' }}</td><td class="num">{{ c.received | taka }}</td><td class="num">{{ c.refunded | taka }}</td><td class="num">{{ c.net | taka }}</td></tr>
                } @empty {
                  <tr><td colspan="4" class="secondary">No transactions.</td></tr>
                }
              </tbody>
            </table>
          </mat-card-content>
        </mat-card>
      </div>

      <mat-card appearance="outlined">
        <mat-card-header><mat-card-title>Transactions ({{ r.transactions.length }})</mat-card-title></mat-card-header>
        <mat-card-content class="scroll">
          <table class="grid wide">
            <thead>
              <tr><th>Time</th><th>Number</th><th>Patient</th><th>Invoice</th><th>Method</th><th>Cashier</th><th class="num">Amount</th></tr>
            </thead>
            <tbody>
              @for (t of r.transactions; track t.id) {
                <tr>
                  <td>{{ t.at | date: 'd MMM, h:mm a' }}</td>
                  <td>
                    @if (t.kind === 'Refund') {
                      {{ t.number }}
                    } @else {
                      <a [routerLink]="['/billing/receipts', t.id, 'print']">{{ t.number }}</a>
                    }
                    <div class="secondary">{{ t.kind === 'Deposit' ? 'Advance deposit' : t.kind }}</div>
                  </td>
                  <td>{{ t.patientName }}<div class="secondary">{{ t.patientMRNumber }}</div></td>
                  <td>{{ t.invoiceNumber ?? '—' }}</td>
                  <td>{{ methods[t.method] }}<div class="secondary">{{ t.reference }}</div></td>
                  <td>{{ t.userName }}</td>
                  <td class="num" [class.out]="t.amount < 0">{{ t.amount | taka }}</td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="secondary">No transactions in this period.</td></tr>
              }
            </tbody>
          </table>
        </mat-card-content>
      </mat-card>
    } @else {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .filters { display: flex; flex-wrap: wrap; align-items: baseline; gap: 12px; }
    .banner { padding: 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }
    .label { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-large); }
    .value { font: var(--mat-sys-headline-small); }
    .net .value { color: var(--mat-sys-primary); }
    .two { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 12px; }
    mat-card-content { padding-top: 8px; }
    .scroll { overflow-x: auto; }
    .grid { width: 100%; border-collapse: collapse; }
    .grid.wide { min-width: 760px; }
    .grid th { text-align: left; font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
    .grid th, .grid td { padding: 6px 4px; border-bottom: 1px solid var(--mat-sys-outline-variant); vertical-align: top; }
    .num { text-align: right !important; white-space: nowrap; }
    .out { color: var(--mat-sys-error); }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class CollectionsPage {
  private readonly api = inject(BillingApiService);

  protected readonly maxDays = MAX_DAYS;
  protected readonly methods = METHOD_LABELS;
  protected from: Date | null = new Date();
  protected to: Date | null = new Date();
  protected cashierId: string | null = null;
  protected readonly report = signal<CollectionReport | null>(null);
  protected readonly error = signal<string | null>(null);
  /** Cashiers seen in the all-cashier report, kept so the filter still lists them after narrowing to one. */
  protected readonly cashiers = signal<{ userId: string | null; name: string | null }[]>([]);
  protected readonly subtitle = computed(() => {
    const r = this.report();
    if (!r) return 'Money received and refunded';
    const period = r.from === r.to ? r.from : `${r.from} to ${r.to}`;
    return `${period} · ${r.allCashiers ? (r.cashierName ?? 'All cashiers') : 'My collection'}`;
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    const from = toIsoDate(this.from);
    const to = toIsoDate(this.to ?? this.from);
    if (!from || !to) {
      this.error.set('Choose the dates.');
      return;
    }
    this.error.set(null);
    this.api.collections(from, to, this.cashierId).subscribe({
      next: (r) => {
        this.report.set(r);
        if (!this.cashierId) {
          this.cashiers.set(r.byCashier.filter((c) => c.userId));
        }
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'The report could not be loaded.')),
    });
  }

  protected print(): void {
    window.print();
  }
}
