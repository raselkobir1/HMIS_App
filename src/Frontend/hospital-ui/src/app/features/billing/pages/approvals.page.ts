import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { Approvals, BillingApiService, BillingPermissions, Refund, REFUND_TYPE_LABELS } from '../billing.models';
import { BillingActions } from '../components/billing-actions';
import { TakaPipe } from '../components/taka.pipe';

/** Discounts and refunds waiting for a decision, and approved refunds to pay out. Route: /billing/approvals. */
@Component({
  selector: 'app-approvals-page',
  imports: [DatePipe, NgTemplateOutlet, RouterLink, MatCardModule, MatButtonModule, MatIconModule, PageHeader, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Billing approvals" subtitle="Discounts above the limit and all refunds need an administrator's approval">
      <button mat-stroked-button type="button" (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
    </app-page-header>

    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    } @else if (data(); as d) {
      <mat-card appearance="outlined">
        <mat-card-header><mat-card-title>Discounts awaiting approval ({{ d.discounts.length }})</mat-card-title></mat-card-header>
        <mat-card-content>
          @for (x of d.discounts; track x.discountId) {
            <div class="row">
              <div class="grow">
                <strong>{{ x.amount | taka }}</strong>{{ x.type === 'Percent' ? ' (' + x.value + '%)' : '' }} on
                <a [routerLink]="['/billing/invoices', x.invoiceId]">{{ x.invoiceNumber }}</a> — {{ x.reason }}
                <span class="secondary">
                  {{ x.patientName }} ({{ x.patientMRNumber }}) · subtotal {{ x.subtotal | taka }}
                  @if (x.discountAlreadyGiven > 0) {
                    · {{ x.discountAlreadyGiven | taka }} already given
                  }
                  · <strong>{{ x.totalPercentOfSubtotal }}% in total</strong> · by {{ x.requestedByName }}, {{ x.requestedAt | date: 'd MMM, h:mm a' }}
                </span>
              </div>
              @if (can.approveDiscount) {
                <button mat-flat-button type="button" (click)="decideDiscount(x.discountId, true)">Approve</button>
                <button mat-button type="button" (click)="decideDiscount(x.discountId, false)">Reject</button>
              }
            </div>
          } @empty {
            <p class="secondary">Nothing waiting.</p>
          }
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined">
        <mat-card-header><mat-card-title>Refunds awaiting approval ({{ d.refundsToApprove.length }})</mat-card-title></mat-card-header>
        <mat-card-content>
          @for (r of d.refundsToApprove; track r.id) {
            <div class="row">
              <div class="grow">
                <ng-container *ngTemplateOutlet="refundText; context: { $implicit: r }" />
              </div>
              @if (can.approveRefund) {
                <button mat-flat-button type="button" (click)="decideRefund(r, true)">Approve</button>
                <button mat-button type="button" (click)="decideRefund(r, false)">Reject</button>
              }
            </div>
          } @empty {
            <p class="secondary">Nothing waiting.</p>
          }
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined">
        <mat-card-header><mat-card-title>Approved refunds to pay out ({{ d.refundsToPay.length }})</mat-card-title></mat-card-header>
        <mat-card-content>
          @for (r of d.refundsToPay; track r.id) {
            <div class="row">
              <div class="grow">
                <ng-container *ngTemplateOutlet="refundText; context: { $implicit: r }" />
                <span class="secondary">Approved by {{ r.decidedByName }}{{ r.decisionNote ? ': ' + r.decisionNote : '' }}</span>
              </div>
              @if (can.payRefund) {
                <button mat-flat-button type="button" (click)="pay(r)">Pay out</button>
              }
            </div>
          } @empty {
            <p class="secondary">Nothing to pay out.</p>
          }
        </mat-card-content>
      </mat-card>

      <ng-template #refundText let-r>
        <strong>{{ r.amount | taka }}</strong> · {{ refundTypes[r.type] }}
        @if (r.invoiceId) {
          <a [routerLink]="['/billing/invoices', r.invoiceId]">{{ r.invoiceNumber }}</a>
        }
        — {{ r.reason }}
        <span class="secondary">
          {{ r.refundNumber }} · <a [routerLink]="['/billing/patients', r.patientId]">{{ r.patientName }} ({{ r.patientMRNumber }})</a> ·
          requested by {{ r.requestedByName }}, {{ r.requestedAt | date: 'd MMM, h:mm a' }}
        </span>
      </ng-template>
    } @else {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .banner { padding: 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    mat-card-content { padding-top: 8px; }
    .row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 10px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .row:last-child { border-bottom: 0; }
    .grow { flex: 1 1 320px; min-width: 0; }
    .secondary { display: block; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .secondary a { color: inherit; }
  `,
})
export class ApprovalsPage {
  private readonly api = inject(BillingApiService);
  private readonly actions = inject(BillingActions);
  private readonly auth = inject(AuthService);

  protected readonly can = {
    approveDiscount: this.auth.hasPermission(BillingPermissions.discountApprove),
    approveRefund: this.auth.hasPermission(BillingPermissions.refundApprove),
    payRefund: this.auth.hasPermission(BillingPermissions.refundRequest),
  };
  protected readonly refundTypes: Record<string, string> = REFUND_TYPE_LABELS;
  protected readonly data = signal<Approvals | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.approvals().subscribe({
      next: (d) => {
        this.data.set(d);
        this.error.set(null);
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'Approvals could not be loaded.')),
    });
  }

  protected decideDiscount(discountId: string, approve: boolean): void {
    this.actions.decideDiscount(discountId, approve).subscribe(() => this.load());
  }

  protected decideRefund(refund: Refund, approve: boolean): void {
    this.actions.decideRefund(refund, approve).subscribe(() => this.load());
  }

  protected pay(refund: Refund): void {
    this.actions.payRefund(refund).subscribe(() => this.load());
  }
}
