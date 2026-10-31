import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { BillingApiService, METHOD_LABELS, MONEY_METHODS, needsReference, PaymentMethod, Refund, RefundType } from '../billing.models';
import { TakaPipe } from './taka.pipe';

export interface RefundRequestDialogData {
  patientId: string;
  patientLabel: string;
  type: RefundType;
  /** For a cancellation refund: the invoice and the cash received for it (all of it is returned). */
  invoice?: { id: string; number: string; cashHeld: number };
  advanceAvailable: number;
  /** The user may approve refunds, so the request is approved at once. */
  canApprove: boolean;
}

/** Requests an advance return or a "cancel and refund" of an invoice. Refunds are paid out only after approval. */
@Component({
  selector: 'app-refund-request-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FieldError, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ cancellation ? 'Cancel invoice and refund' : 'Return advance' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">{{ data.patientLabel }}</p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        @if (data.invoice; as i) {
          <p class="span">
            All cash received for <strong>{{ i.number }}</strong> — <strong>{{ i.cashHeld | taka }}</strong> — will be returned and the
            invoice cancelled once the refund is paid out. Advance applied to it goes back to the patient's advance.
          </p>
        } @else {
          <mat-form-field>
            <mat-label>Amount (৳)</mat-label>
            <input matInput type="number" step="0.01" min="0" formControlName="amount" cdkFocusInitial />
            <mat-hint>Available {{ data.advanceAvailable | taka }}</mat-hint>
            <mat-error><app-field-error [control]="form.controls.amount" /></mat-error>
          </mat-form-field>
        }
        <mat-form-field class="span">
          <mat-label>Reason</mat-label>
          <input matInput formControlName="reason" maxlength="250" />
          <mat-error><app-field-error [control]="form.controls.reason" /></mat-error>
        </mat-form-field>
        <p class="span hint">
          {{ data.canApprove ? 'You can approve refunds, so this request is approved at once.' : 'An administrator must approve the refund before it is paid out.' }}
        </p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Close</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Request refund</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class RefundRequestDialog {
  protected readonly data = inject<RefundRequestDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RefundRequestDialog, Refund>);
  private readonly api = inject(BillingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly cancellation = this.data.type === 'InvoiceCancellation';
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    amount: new FormControl<number | null>(null, this.cancellation ? [] : [Validators.required, Validators.min(0.01), Validators.max(this.data.advanceAvailable)]),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(250)] }),
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .requestRefund({
        patientId: this.data.patientId,
        type: this.data.type,
        invoiceId: this.data.invoice?.id ?? null,
        amount: this.cancellation ? null : v.amount,
        reason: v.reason.trim(),
      })
      .subscribe({
        next: (refund) => {
          this.notifications.success(refund.status === 'Approved' ? `Refund ${refund.refundNumber} approved; pay it out next.` : `Refund ${refund.refundNumber} sent for approval.`);
          this.dialogRef.close(refund);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}

/** Records how an approved refund was handed back. */
@Component({
  selector: 'app-refund-pay-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Pay out refund</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        <p class="span subtitle">
          {{ refund.refundNumber }} · {{ refund.patientName }} ({{ refund.patientMRNumber }})<br />
          Return <strong>{{ refund.amount | taka }}</strong>{{ refund.invoiceNumber ? ' for ' + refund.invoiceNumber : '' }}
        </p>
        @if (error(); as message) {
          <div class="banner span" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Paid by</mat-label>
          <mat-select formControlName="method">
            @for (m of methods; track m) {
              <mat-option [value]="m">{{ labels[m] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (referenceNeeded()) {
          <mat-form-field>
            <mat-label>Transaction reference</mat-label>
            <input matInput formControlName="reference" maxlength="100" />
            <mat-error><app-field-error [control]="form.controls.reference" /></mat-error>
          </mat-form-field>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Mark as paid</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class RefundPayDialog {
  protected readonly refund = inject<Refund>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RefundPayDialog, Refund>);
  private readonly api = inject(BillingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly labels = METHOD_LABELS;
  protected readonly methods = MONEY_METHODS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    method: new FormControl<PaymentMethod>('Cash', { nonNullable: true }),
    reference: new FormControl<string | null>(null, Validators.maxLength(100)),
  });
  private readonly method = toSignal(this.form.controls.method.valueChanges, { initialValue: this.form.controls.method.value });
  protected readonly referenceNeeded = computed(() => needsReference(this.method()));

  protected save(): void {
    const v = this.form.getRawValue();
    if (needsReference(v.method) && !v.reference?.trim()) {
      this.form.controls.reference.setErrors({ required: true });
      this.form.controls.reference.markAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api.payRefund(this.refund.id, v.method, needsReference(v.method) ? v.reference!.trim() : null).subscribe({
      next: (refund) => {
        this.notifications.success(`Refund ${refund.refundNumber} paid.`);
        this.dialogRef.close(refund);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}
