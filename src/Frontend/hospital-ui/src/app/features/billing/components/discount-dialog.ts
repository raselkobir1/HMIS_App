import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { map } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { BillingApiService, DiscountType, Invoice } from '../billing.models';
import { TakaPipe } from './taka.pipe';

export interface DiscountDialogData {
  invoice: Invoice;
  /** The user may approve discounts above the limit themselves. */
  canApprove: boolean;
}

/** Requests a discount; shows the amount and whether it applies now or waits for approval. */
@Component({
  selector: 'app-discount-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatButtonToggleModule, FieldError, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Discount</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">
          {{ data.invoice.invoiceNumber }} · subtotal {{ data.invoice.subtotal | taka }} · due {{ data.invoice.dueAmount | taka }}
          @if (data.invoice.discountAmount > 0) {
            <br />Discount already given: {{ data.invoice.discountAmount | taka }}
          }
        </p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-button-toggle-group formControlName="type" aria-label="Discount type" class="span">
          <mat-button-toggle value="Amount">Amount (৳)</mat-button-toggle>
          <mat-button-toggle value="Percent">Percentage (%)</mat-button-toggle>
        </mat-button-toggle-group>
        <mat-form-field>
          <mat-label>{{ values().type === 'Percent' ? 'Percentage' : 'Amount (৳)' }}</mat-label>
          <input matInput type="number" step="0.01" min="0" formControlName="value" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.value" /></mat-error>
        </mat-form-field>
        <div class="preview" aria-live="polite">
          @if (amount() > 0) {
            <div>Discount: <strong>{{ amount() | taka }}</strong></div>
            <div class="secondary">Total {{ totalPercent() }}% of subtotal</div>
          }
        </div>
        <mat-form-field class="span">
          <mat-label>Reason</mat-label>
          <input matInput formControlName="reason" maxlength="250" />
          <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
        </mat-form-field>
        @if (amount() > data.invoice.dueAmount) {
          <div class="banner span">The discount cannot be more than the amount due.</div>
        } @else if (needsApproval()) {
          <div class="note span">
            Above {{ limitValue() }}% in total{{ data.canApprove ? ': you can approve this yourself, so it applies at once.' : ': it will wait for an administrator’s approval.' }}
          </div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving() || amount() > data.invoice.dueAmount">
          {{ needsApproval() && !data.canApprove ? 'Send for approval' : 'Apply discount' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }
    .preview { align-self: center; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .note { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    mat-button-toggle-group { margin-bottom: 8px; width: fit-content; }
  `,
})
export class DiscountDialog {
  protected readonly data = inject<DiscountDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DiscountDialog, Invoice>);
  private readonly api = inject(BillingApiService);
  private readonly notifications = inject(NotificationService);

  /** From Billing settings; 10% until loaded (the server applies the real limit either way). */
  protected readonly limitValue = toSignal(this.api.settings().pipe(map((s) => s.discountApprovalPercent)), { initialValue: 10 });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    type: new FormControl<DiscountType>('Amount', { nonNullable: true }),
    value: new FormControl<number | null>(null, [Validators.required, Validators.min(0.01)]),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(250)] }),
  });

  protected readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly amount = computed(() => {
    const { type, value } = this.values();
    if (!value || value <= 0) return 0;
    const raw = type === 'Percent' ? (this.data.invoice.subtotal * Math.min(value, 100)) / 100 : value;
    return Math.round(raw * 100) / 100;
  });
  protected readonly totalPercent = computed(() =>
    this.data.invoice.subtotal > 0
      ? Math.round(((this.data.invoice.discountAmount + this.amount()) * 1000) / this.data.invoice.subtotal) / 10
      : 0,
  );
  protected readonly needsApproval = computed(() => this.totalPercent() > this.limitValue());

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api.requestDiscount(this.data.invoice.id, v.type, v.value!, v.reason.trim()).subscribe({
      next: (invoice) => {
        const latest = invoice.discounts[invoice.discounts.length - 1];
        this.notifications.success(latest?.status === 'Pending' ? 'Discount sent for approval.' : 'Discount applied.');
        this.dialogRef.close(invoice);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}
