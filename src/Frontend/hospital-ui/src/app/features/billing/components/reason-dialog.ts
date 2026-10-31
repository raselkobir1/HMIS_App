import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { FieldError } from '../../../shared/components/field-error/field-error';

export interface ReasonDialogData {
  title: string;
  message?: string;
  label: string;
  confirmText: string;
  required: boolean;
  destructive?: boolean;
}

/** Asks for a reason or note before an action; closes with the text (empty string when optional and blank). */
@Component({
  selector: 'app-reason-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <!-- [formGroup] makes (ngSubmit) fire; a bare <form> would do a native submit and reload the page. -->
    <form [formGroup]="form" (ngSubmit)="confirm()" novalidate>
      <mat-dialog-content>
        @if (data.message) {
          <p>{{ data.message }}</p>
        }
        <mat-form-field class="full">
          <mat-label>{{ data.label }}</mat-label>
          <input matInput formControlName="text" maxlength="250" cdkFocusInitial />
          <mat-error><app-field-error [control]="text" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <!-- Close with no result: an empty string would read as "confirmed without a note". -->
        <button mat-button type="button" [mat-dialog-close]="undefined">Cancel</button>
        <button mat-flat-button type="submit" [class.destructive]="data.destructive">{{ data.confirmText }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .full { width: 100%; min-width: min(420px, 80vw); }
    .destructive {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class ReasonDialog {
  protected readonly data = inject<ReasonDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ReasonDialog, string>);
  protected readonly text = new FormControl('', {
    nonNullable: true,
    validators: this.data.required ? [Validators.required, Validators.maxLength(250)] : [Validators.maxLength(250)],
  });
  protected readonly form = new FormGroup({ text: this.text });

  protected confirm(): void {
    if (this.text.invalid) {
      this.text.markAsTouched();
      return;
    }
    this.dialogRef.close(this.text.value.trim());
  }
}
