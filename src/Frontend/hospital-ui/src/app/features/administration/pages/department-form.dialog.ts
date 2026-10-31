import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { applyServerErrors } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../administration-api.service';
import { Department, DEPARTMENT_TYPES, DepartmentLookupItem, DepartmentType } from '../administration.models';

@Component({
  selector: 'app-department-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ department ? 'Edit department' : 'New department' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (bannerError(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Code</mat-label>
          <input matInput formControlName="code" class="upper" />
          <mat-error><app-field-error [control]="form.controls.code" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Type</mat-label>
          <mat-select formControlName="type">
            @for (t of types; track t) {
              <mat-option [value]="t">{{ t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="full span">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" />
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        <mat-form-field class="full span">
          <mat-label>Parent department</mat-label>
          <mat-select formControlName="parentDepartmentId">
            <mat-option [value]="null">None (top-level department)</mat-option>
            @for (p of parents(); track p.id) {
              <mat-option [value]="p.id">{{ p.name }} ({{ p.code }})</mat-option>
            }
          </mat-select>
          <mat-hint>Only top-level departments can have sub-departments.</mat-hint>
          <mat-error><app-field-error [control]="form.controls.parentDepartmentId" /></mat-error>
        </mat-form-field>
        <mat-form-field class="full span">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="2"></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ department ? 'Save' : 'Create' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .span {
      grid-column: 1 / -1;
    }
    .upper {
      text-transform: uppercase;
    }
  `,
})
export class DepartmentFormDialog {
  protected readonly department = inject<Department | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DepartmentFormDialog, boolean>);
  private readonly api = inject(AdministrationApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly types = DEPARTMENT_TYPES;
  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  private readonly lookup = signal<DepartmentLookupItem[]>([]);

  /** Eligible parents: active top-level departments other than this one. */
  protected readonly parents = computed(() =>
    this.lookup().filter((d) => d.parentDepartmentId === null && d.id !== this.department?.id),
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    code: [this.department?.code ?? '', [Validators.required, Validators.minLength(2), Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9_-]+$/)]],
    name: [this.department?.name ?? '', [Validators.required, Validators.maxLength(150)]],
    type: [this.department?.type ?? ('Clinical' as DepartmentType), Validators.required],
    parentDepartmentId: [this.department?.parentDepartmentId ?? (null as string | null)],
    description: [this.department?.description ?? '', Validators.maxLength(500)],
  });

  constructor() {
    this.api.departmentLookup().subscribe((items) => this.lookup.set(items));
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    const v = this.form.getRawValue();
    const request = { ...v, description: v.description.trim() || null };

    const request$ = this.department
      ? this.api.updateDepartment(this.department.id, { ...request, rowVersion: this.department.rowVersion })
      : this.api.createDepartment(request);

    request$.subscribe({
      next: () => {
        this.notifications.success(this.department ? 'Department updated.' : 'Department created.');
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
