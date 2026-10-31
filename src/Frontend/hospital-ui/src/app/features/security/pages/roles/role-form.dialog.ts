import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { NotificationService } from '../../../../core/services/notification.service';
import { FieldError } from '../../../../shared/components/field-error/field-error';
import { applyServerErrors } from '../../../../shared/utilities/server-errors';
import { RoleDetail } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';

/** Create (data = null) or edit a role's name and description. Permissions are edited on their own page. */
@Component({
  selector: 'app-role-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ role ? 'Edit role' : 'New role' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (bannerError(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" [readonly]="role?.isSystem" />
          @if (role?.isSystem) {
            <mat-hint>System roles cannot be renamed.</mat-hint>
          }
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
          <mat-error><app-field-error [control]="form.controls.description" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ role ? 'Save' : 'Create role' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../../shared/styles/dialog.scss',
})
export class RoleFormDialog {
  protected readonly role = inject<RoleDetail | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RoleFormDialog, boolean>);
  private readonly api = inject(SecurityApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.role?.name ?? '', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    description: [this.role?.description ?? '', Validators.maxLength(250)],
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const { name, description } = this.form.getRawValue();
    const request$ = this.role
      ? this.api.updateRole(this.role.id, { name, description: description || null, rowVersion: this.role.rowVersion })
      : this.api.createRole({ name, description: description || null, permissionIds: [] });

    request$.subscribe({
      next: () => {
        this.notifications.success(this.role ? 'Role updated.' : 'Role created. Now assign its permissions.');
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
