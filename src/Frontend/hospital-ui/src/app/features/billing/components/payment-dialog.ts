import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { BillingApiService, METHOD_LABELS, MONEY_METHODS, needsReference, PaymentMethod, Receipt } from '../billing.models';
import { TakaPipe } from './taka.pipe';

export interface PaymentDialogData {
  patientId: string;
  patientLabel: string;
  /** Null for an advance deposit. */
  invoice: { id: string; number: string; due: number } | null;
  advanceAvailable: number;
}

/**
 * Receives a payment against an invoice, or an advance deposit. One request id per dialog: if the connection drops
 * after the server saved the payment, submitting again returns the same receipt instead of charging twice.
 */
@Component({
  selector: 'app-payment-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.invoice ? 'Receive payment' : 'Advance deposit' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">
          {{ data.patientLabel }}
          @if (data.invoice; as i) {
            <br />{{ i.number }} · due <strong>{{ i.due | taka }}</strong>
          }
          @if (data.advanceAvailable > 0) {
            <br />Advance available: {{ data.advanceAvailable | taka }}
          }
        </p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Method</mat-label>
          <mat-select formControlName="method">
            @for (m of methods; track m) {
              <mat-option [value]="m">{{ labels[m] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Amount (৳)</mat-label>
          <input matInput type="number" step="0.01" min="0" formControlName="amount" cdkFocusInitial />
          <mat-hint>{{ limitHint() }}</mat-hint>
          <mat-error><app-field-error [control]="form.controls.amount" /></mat-error>
        </mat-form-field>
        @if (referenceNeeded()) {
          <mat-form-field class="span">
            <mat-label>Transaction / slip reference</mat-label>
            <input matInput formControlName="reference" maxlength="100" />
            <mat-error><app-field-error [control]="form.controls.reference" /></mat-error>
          </mat-form-field>
        }
        <mat-form-field class="span">
          <mat-label>Notes (optional)</mat-label>
          <input matInput formControlName="notes" maxlength="250" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ data.invoice ? 'Receive payment' : 'Receive deposit' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class PaymentDialog {
  protected readonly data = inject<PaymentDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<PaymentDialog, Receipt>);
  private readonly api = inject(BillingApiService);
  private readonly notifications = inject(NotificationService);
  private readonly requestId = crypto.randomUUID();

  protected readonly labels = METHOD_LABELS;
  protected readonly methods: PaymentMethod[] =
    this.data.invoice && this.data.advanceAvailable > 0 ? [...MONEY_METHODS, 'Advance'] : MONEY_METHODS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    method: new FormControl<PaymentMethod>('Cash', { nonNullable: true }),
    amount: new FormControl<number | null>(this.data.invoice?.due ?? null, [Validators.required, Validators.min(0.01)]),
    reference: new FormControl<string | null>(null, Validators.maxLength(100)),
    notes: new FormControl<string | null>(null, Validators.maxLength(250)),
  });

  private readonly method = toSignal(this.form.controls.method.valueChanges, { initialValue: this.form.controls.method.value });
  protected readonly referenceNeeded = computed(() => needsReference(this.method()));
  private readonly limit = computed(() => {
    const invoice = this.data.invoice;
    if (!invoice) return null;
    return this.method() === 'Advance' ? Math.min(invoice.due, this.data.advanceAvailable) : invoice.due;
  });
  protected readonly limitHint = computed(() => {
    const limit = this.limit();
    return limit === null ? '' : `At most ${limit.toFixed(2)}`;
  });

  constructor() {
    this.form.controls.method.valueChanges.pipe(takeUntilDestroyed()).subscribe((method) => {
      const amount = this.form.controls.amount;
      const limit = this.limit();
      amount.setValidators([Validators.required, Validators.min(0.01), ...(limit === null ? [] : [Validators.max(limit)])]);
      if (method === 'Advance' && limit !== null && (amount.value ?? 0) > limit) {
        amount.setValue(limit);
      }
      amount.updateValueAndValidity();
      const reference = this.form.controls.reference;
      reference.setValidators(needsReference(method) ? [Validators.required, Validators.maxLength(100)] : [Validators.maxLength(100)]);
      reference.updateValueAndValidity();
    });
    this.form.controls.method.setValue('Cash');
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .collect({
        clientRequestId: this.requestId,
        patientId: this.data.patientId,
        invoiceId: this.data.invoice?.id ?? null,
        amount: Math.round(v.amount! * 100) / 100,
        method: v.method,
        reference: needsReference(v.method) ? v.reference?.trim() || null : null,
        notes: v.notes?.trim() || null,
      })
      .subscribe({
        next: (receipt) => {
          this.notifications.success(`Received ${receipt.payment.amount.toFixed(2)} — receipt ${receipt.payment.receiptNumber}.`);
          this.dialogRef.close(receipt);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}
