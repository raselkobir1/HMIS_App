import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NotificationService } from '../../../../core/services/notification.service';
import { applyServerErrors } from '../../../../shared/utilities/server-errors';
import { RoleListItem, UserDetail } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';
import { passwordPolicyValidator } from '../../validators/password-policy.validator';
import { FieldError } from '../../../../shared/components/field-error/field-error';

export type UserFormDialogData = { mode: 'create' } | { mode: 'edit'; user: UserDetail };

@Component({
  selector: 'app-user-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit user' : 'New user' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (bannerError(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }

        <mat-form-field>
          <mat-label>Username</mat-label>
          <input matInput formControlName="userName" autocomplete="off" [readonly]="isEdit" />
          @if (isEdit) {
            <mat-hint>Username cannot be changed.</mat-hint>
          }
          <mat-error><app-field-error [control]="form.controls.userName" /></mat-error>
        </mat-form-field>

        <mat-form-field>
          <mat-label>Full name</mat-label>
          <input matInput formControlName="fullName" />
          <mat-error><app-field-error [control]="form.controls.fullName" /></mat-error>
        </mat-form-field>

        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" />
          <mat-error><app-field-error [control]="form.controls.email" /></mat-error>
        </mat-form-field>

        <mat-form-field>
          <mat-label>Phone number</mat-label>
          <input matInput type="tel" formControlName="phoneNumber" />
          <mat-error><app-field-error [control]="form.controls.phoneNumber" /></mat-error>
        </mat-form-field>

        @if (!isEdit) {
          <mat-form-field>
            <mat-label>Temporary password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" />
            <mat-hint>The user must change it at first sign-in.</mat-hint>
            <mat-error><app-field-error [control]="form.controls.password" /></mat-error>
          </mat-form-field>

          <mat-form-field>
            <mat-label>Roles</mat-label>
            <mat-select formControlName="roleIds" multiple>
              @for (role of roles(); track role.id) {
                <mat-option [value]="role.id">{{ role.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ isEdit ? 'Save' : 'Create user' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../dialog.scss',
})
export class UserFormDialog {
  private readonly data = inject<UserFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<UserFormDialog, boolean>);
  private readonly api = inject(SecurityApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly isEdit = this.data.mode === 'edit';
  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  protected readonly roles = signal<RoleListItem[]>([]);

  protected readonly form = inject(NonNullableFormBuilder).group({
    userName: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50), Validators.pattern(/^[A-Za-z0-9._-]+$/)]],
    fullName: ['', [Validators.required, Validators.maxLength(150)]],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    phoneNumber: ['', [Validators.maxLength(20), Validators.pattern(/^\+?[0-9\s-]{6,20}$/)]],
    password: ['', this.isEdit ? [] : [passwordPolicyValidator]],
    roleIds: [[] as string[]],
  });

  constructor() {
    if (this.data.mode === 'edit') {
      const user = this.data.user;
      this.form.patchValue({
        userName: user.userName,
        fullName: user.fullName,
        email: user.email ?? '',
        phoneNumber: user.phoneNumber ?? '',
      });
    } else {
      this.api.searchRoles().subscribe((page) => this.roles.set(page.items));
    }
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    const value = this.form.getRawValue();

    const request$ =
      this.data.mode === 'edit'
        ? this.api.updateUser(this.data.user.id, {
            fullName: value.fullName,
            email: value.email || null,
            phoneNumber: value.phoneNumber || null,
            employeeId: this.data.user.employeeId,
            rowVersion: this.data.user.rowVersion,
          })
        : this.api.createUser({
            userName: value.userName,
            fullName: value.fullName,
            email: value.email || null,
            phoneNumber: value.phoneNumber || null,
            password: value.password,
            roleIds: value.roleIds,
          });

    request$.subscribe({
      next: () => {
        this.notifications.success(this.isEdit ? 'User updated.' : 'User created.');
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
