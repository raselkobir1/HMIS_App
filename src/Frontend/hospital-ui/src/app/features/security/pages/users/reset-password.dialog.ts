import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { NotificationService } from '../../../../core/services/notification.service';
import { FieldError } from '../../../../shared/components/field-error/field-error';
import { applyServerErrors } from '../../../../shared/utilities/server-errors';
import { UserListItem } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';
import { passwordPolicyValidator } from '../../validators/password-policy.validator';

@Component({
  selector: 'app-reset-password-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Reset password</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        <p>
          Set a temporary password for <strong>{{ user.fullName }}</strong>. They will be signed out everywhere and
          must change it at next sign-in.
        </p>
        @if (bannerError(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Temporary password</mat-label>
          <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
          <mat-error><app-field-error [control]="form.controls.newPassword" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Reset password</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../../shared/styles/dialog.scss',
})
export class ResetPasswordDialog {
  protected readonly user = inject<UserListItem>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ResetPasswordDialog, boolean>);
  private readonly api = inject(SecurityApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({ newPassword: ['', passwordPolicyValidator] });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.api.resetPassword(this.user.id, this.form.getRawValue().newPassword).subscribe({
      next: () => {
        this.notifications.success('Password reset. The user must change it at next sign-in.');
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
