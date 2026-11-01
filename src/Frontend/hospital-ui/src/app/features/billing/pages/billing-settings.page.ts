import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { BillingApiService, BillingSettings, METHOD_LABELS, PaymentMethod } from '../billing.models';

/** Cash and advance deposits are always available. */
const FIXED: PaymentMethod[] = ['Cash', 'Advance'];

/** Discount approval limit and the payment methods offered at the counter. Route: /billing/settings. */
@Component({
  selector: 'app-billing-settings-page',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatCheckboxModule, MatButtonModule, FieldError, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Billing Settings" subtitle="Discount approval and payment methods" />
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    @if (settings(); as s) {
      <form [formGroup]="form" (ngSubmit)="save(s)" novalidate>
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Discount approval</mat-card-title></mat-card-header>
          <mat-card-content>
            <mat-form-field>
              <mat-label>Approval needed above (%)</mat-label>
              <input matInput type="number" formControlName="discountApprovalPercent" min="0" max="100" step="0.5" />
              <mat-hint>Discounts up to this share of a bill apply at once; larger ones wait for an approver.</mat-hint>
              <mat-error><app-field-error [control]="form.controls.discountApprovalPercent" /></mat-error>
            </mat-form-field>
          </mat-card-content>
        </mat-card>
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Payment methods</mat-card-title></mat-card-header>
          <mat-card-content class="methods">
            @for (m of s.allMethods; track m) {
              <mat-checkbox [checked]="enabled().has(m)" [disabled]="fixed.includes(m)" (change)="toggle(m, $event.checked)">
                {{ labels[m] }}{{ fixed.includes(m) ? ' (always on)' : '' }}
              </mat-checkbox>
            }
          </mat-card-content>
        </mat-card>
        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="saving()">Save settings</button>
        </div>
      </form>
    } @else if (!error()) {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; max-width: 720px; }
    form { display: flex; flex-direction: column; gap: 12px; }
    mat-card-content { padding-top: 12px; }
    mat-form-field { width: 100%; max-width: 360px; }
    .methods { display: flex; flex-wrap: wrap; gap: 4px 24px; }
    .actions { display: flex; justify-content: flex-end; }
    .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .secondary { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class BillingSettingsPage implements OnInit {
  private readonly api = inject(BillingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly labels = METHOD_LABELS;
  protected readonly fixed = FIXED;
  protected readonly settings = signal<BillingSettings | null>(null);
  protected readonly enabled = signal(new Set<PaymentMethod>());
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly form = new FormGroup({
    discountApprovalPercent: new FormControl<number | null>(null, [Validators.required, Validators.min(0), Validators.max(100)]),
  });

  ngOnInit(): void {
    this.api.settings().subscribe({
      next: (s) => this.apply(s),
      error: (err) => this.error.set(apiErrorMessage(err)),
    });
  }

  protected toggle(method: PaymentMethod, on: boolean): void {
    this.enabled.update((set) => {
      const next = new Set(set);
      if (on) {
        next.add(method);
      } else {
        next.delete(method);
      }
      return next;
    });
  }

  protected save(s: BillingSettings): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api
      .updateSettings({
        discountApprovalPercent: this.form.getRawValue().discountApprovalPercent!,
        enabledMethods: [...this.enabled()],
        rowVersion: s.rowVersion,
      })
      .subscribe({
        next: (saved) => {
          this.saving.set(false);
          this.apply(saved);
          this.notifications.success('Billing settings saved.');
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err));
        },
      });
  }

  private apply(s: BillingSettings): void {
    this.settings.set(s);
    this.enabled.set(new Set(s.enabledMethods));
    this.form.setValue({ discountApprovalPercent: s.discountApprovalPercent });
  }
}
