import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { BillableService, BillingApiService, SERVICE_CATEGORIES, ServiceCategory } from '../billing.models';

/** Adds or edits a price-list entry. The code is fixed once created (it is printed on invoices). */
@Component({
  selector: 'app-service-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ service ? 'Edit service' : 'Add service' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Code</mat-label>
          <input matInput formControlName="code" maxlength="20" class="upper" />
          <mat-hint>Letters, digits and hyphens</mat-hint>
          <mat-error><app-field-error [control]="form.controls.code" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Category</mat-label>
          <mat-select formControlName="category">
            @for (c of categories; track c) {
              <mat-option [value]="c">{{ c }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="150" />
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Price (৳)</mat-label>
          <input matInput type="number" step="0.01" min="0" formControlName="price" />
          <mat-error><app-field-error [control]="form.controls.price" /></mat-error>
        </mat-form-field>
        <mat-checkbox formControlName="isPriceEditable">Cashier may change the price</mat-checkbox>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span { grid-column: 1 / -1; }
    .upper { text-transform: uppercase; }
    mat-checkbox { align-self: center; }
  `,
})
export class ServiceDialog {
  protected readonly service = inject<BillableService | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ServiceDialog, BillableService>);
  private readonly api = inject(BillingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly categories = SERVICE_CATEGORIES;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    code: new FormControl(
      { value: this.service?.code ?? '', disabled: !!this.service },
      { nonNullable: true, validators: [Validators.required, Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9-]+$/)] },
    ),
    name: new FormControl(this.service?.name ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(150)] }),
    category: new FormControl<ServiceCategory>(this.service?.category ?? 'Procedure', { nonNullable: true }),
    price: new FormControl<number | null>(this.service?.price ?? null, [Validators.required, Validators.min(0)]),
    isPriceEditable: new FormControl(this.service?.isPriceEditable ?? false, { nonNullable: true }),
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = { name: v.name.trim(), category: v.category, price: v.price!, isPriceEditable: v.isPriceEditable };
    const request$ = this.service
      ? this.api.updateService(this.service.id, { ...body, rowVersion: this.service.rowVersion })
      : this.api.createService({ ...body, code: v.code.trim() });
    this.saving.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (saved) => {
        this.notifications.success(this.service ? 'Service updated.' : 'Service added.');
        this.dialogRef.close(saved);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
      },
    });
  }
}
