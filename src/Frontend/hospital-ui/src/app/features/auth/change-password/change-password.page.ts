import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { applyServerErrors } from '../../../shared/utilities/server-errors';
import { passwordPolicyValidator } from '../../security/validators/password-policy.validator';

const matchNewPassword = (group: AbstractControl): ValidationErrors | null => {
  const confirm = group.get('confirmPassword');
  const mismatch = group.get('newPassword')?.value !== confirm?.value;
  if (confirm && mismatch !== confirm.hasError('mismatch')) {
    confirm.setErrors(mismatch ? { ...(confirm.errors ?? {}), mismatch: true } : null);
  }
  return null;
};

@Component({
  selector: 'app-change-password-page',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, PageHeader, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Change password" />
    @if (forced()) {
      <div class="notice" role="status">You must set a new password before you can continue.</div>
    }
    <mat-card appearance="outlined" class="card">
      <mat-card-content>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (bannerError(); as message) {
            <div class="banner" role="alert">{{ message }}</div>
          }
          <mat-form-field>
            <mat-label>Current password</mat-label>
            <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
            <mat-error><app-field-error [control]="form.controls.currentPassword" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>New password</mat-label>
            <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
            <mat-hint>At least 8 characters with uppercase, lowercase and a digit.</mat-hint>
            <mat-error><app-field-error [control]="form.controls.newPassword" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Confirm new password</mat-label>
            <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
            <mat-error><app-field-error [control]="form.controls.confirmPassword" /></mat-error>
          </mat-form-field>
          <button mat-flat-button type="submit" [disabled]="saving()">Change password</button>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .card {
      max-width: 480px;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding-top: 8px;
    }
    .notice,
    .banner {
      padding: 12px;
      margin-bottom: 16px;
      border-radius: 8px;
      max-width: 456px;
    }
    .notice {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .banner {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class ChangePasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly forced = this.auth.mustChangePassword;
  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', passwordPolicyValidator],
      confirmPassword: ['', Validators.required],
    },
    { validators: matchNewPassword },
  );

  protected submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    const { currentPassword, newPassword } = this.form.getRawValue();

    this.auth.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.notifications.success('Password changed.');
        void this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
