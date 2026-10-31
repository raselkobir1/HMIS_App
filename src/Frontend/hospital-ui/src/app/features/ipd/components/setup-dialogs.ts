import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { catchError, of } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../../administration/administration-api.service';
import { Bed, BedCategory, IpdApiService, Ward, WARD_TYPE_LABELS, WardType } from '../ipd.models';

const DIALOG_IMPORTS = [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError];
const DIALOG_STYLES = `.span { grid-column: 1 / -1; } .upper { text-transform: uppercase; }`;

/** Bed category: daily rate and suggested admission deposit. */
@Component({
  selector: 'app-bed-category-dialog',
  imports: DIALOG_IMPORTS,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ category ? 'Edit bed category' : 'Add bed category' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="span">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="100" placeholder="e.g. General ward bed, Single cabin AC" />
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Daily rate (৳)</mat-label>
          <input matInput type="number" min="0" step="0.01" formControlName="dailyRate" />
          <mat-error><app-field-error [control]="form.controls.dailyRate" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Suggested deposit (৳)</mat-label>
          <input matInput type="number" min="0" step="0.01" formControlName="suggestedDeposit" />
          <mat-error><app-field-error [control]="form.controls.suggestedDeposit" /></mat-error>
        </mat-form-field>
        @if (category) {
          <p class="span hint">A new rate applies to bed days not yet charged; days already on a bill keep their price.</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: DIALOG_STYLES,
})
export class BedCategoryDialog {
  protected readonly category = inject<BedCategory | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<BedCategoryDialog, BedCategory>);
  private readonly api = inject(IpdApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    name: new FormControl(this.category?.name ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    dailyRate: new FormControl<number | null>(this.category?.dailyRate ?? null, [Validators.required, Validators.min(0)]),
    suggestedDeposit: new FormControl<number | null>(this.category?.suggestedDeposit ?? 0, [Validators.required, Validators.min(0)]),
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api
      .saveCategory(this.category?.id ?? null, {
        name: v.name.trim(),
        dailyRate: v.dailyRate!,
        suggestedDeposit: v.suggestedDeposit!,
        rowVersion: this.category?.rowVersion,
      })
      .subscribe({
        next: (saved) => {
          this.notifications.success('Bed category saved.');
          this.dialogRef.close(saved);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}

/** Ward, cabin block or unit. The code is fixed once created. */
@Component({
  selector: 'app-ward-dialog',
  imports: DIALOG_IMPORTS,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ ward ? 'Edit ward' : 'Add ward' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field>
          <mat-label>Code</mat-label>
          <input matInput formControlName="code" maxlength="20" class="upper" />
          <mat-error><app-field-error [control]="form.controls.code" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Type</mat-label>
          <mat-select formControlName="type">
            @for (t of types; track t) {
              <mat-option [value]="t">{{ typeLabels[t] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="100" />
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Department (optional)</mat-label>
          <mat-select formControlName="departmentId">
            <mat-option [value]="null">None</mat-option>
            @for (d of departments(); track d.id) {
              <mat-option [value]="d.id">{{ d.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Building</mat-label>
          <input matInput formControlName="building" maxlength="100" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Floor</mat-label>
          <input matInput formControlName="floor" maxlength="30" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: DIALOG_STYLES,
})
export class WardDialog {
  protected readonly ward = inject<Ward | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<WardDialog, Ward>);
  private readonly api = inject(IpdApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly types = Object.keys(WARD_TYPE_LABELS) as WardType[];
  protected readonly typeLabels = WARD_TYPE_LABELS;
  protected readonly departments = toSignal(inject(AdministrationApiService).departmentLookup().pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    code: new FormControl(
      { value: this.ward?.code ?? '', disabled: !!this.ward },
      { nonNullable: true, validators: [Validators.required, Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9-]+$/)] },
    ),
    name: new FormControl(this.ward?.name ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    type: new FormControl<WardType>(this.ward?.type ?? 'General', { nonNullable: true }),
    departmentId: new FormControl<string | null>(this.ward?.departmentId ?? null),
    building: new FormControl<string | null>(this.ward?.building ?? null),
    floor: new FormControl<string | null>(this.ward?.floor ?? null),
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api
      .saveWard(this.ward?.id ?? null, {
        code: v.code.trim(),
        name: v.name.trim(),
        type: v.type,
        departmentId: v.departmentId,
        building: v.building?.trim() || null,
        floor: v.floor?.trim() || null,
        rowVersion: this.ward?.rowVersion ?? 0,
      })
      .subscribe({
        next: (saved) => {
          this.notifications.success('Ward saved.');
          this.dialogRef.close(saved);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}

export interface BedDialogData {
  bed: Bed | null;
  wards: Ward[];
  categories: BedCategory[];
}

/** A bed in a ward. The ward is fixed once created. */
@Component({
  selector: 'app-bed-dialog',
  imports: DIALOG_IMPORTS,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.bed ? 'Edit bed' : 'Add bed' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="span">
          <mat-label>Ward</mat-label>
          <mat-select formControlName="wardId">
            @for (w of data.wards; track w.id) {
              <mat-option [value]="w.id" [disabled]="!w.isActive">{{ w.name }}</mat-option>
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.wardId" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Bed number</mat-label>
          <input matInput formControlName="bedNumber" maxlength="20" class="upper" />
          <mat-error><app-field-error [control]="form.controls.bedNumber" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Room / cabin (optional)</mat-label>
          <input matInput formControlName="room" maxlength="30" />
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Category</mat-label>
          <mat-select formControlName="categoryId">
            @for (c of data.categories; track c.id) {
              @if (c.isActive || c.id === data.bed?.categoryId) {
                <mat-option [value]="c.id">{{ c.name }} · ৳{{ c.dailyRate }}/day</mat-option>
              }
            }
          </mat-select>
          <mat-error><app-field-error [control]="form.controls.categoryId" /></mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: DIALOG_STYLES,
})
export class BedDialog {
  protected readonly data = inject<BedDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<BedDialog, Bed>);
  private readonly api = inject(IpdApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    wardId: new FormControl({ value: this.data.bed?.wardId ?? '', disabled: !!this.data.bed }, { nonNullable: true, validators: Validators.required }),
    bedNumber: new FormControl(this.data.bed?.bedNumber ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(20)] }),
    room: new FormControl<string | null>(this.data.bed?.room ?? null),
    categoryId: new FormControl(this.data.bed?.categoryId ?? '', { nonNullable: true, validators: Validators.required }),
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api
      .saveBed(this.data.bed?.id ?? null, {
        wardId: v.wardId,
        bedNumber: v.bedNumber.trim(),
        room: v.room?.trim() || null,
        categoryId: v.categoryId,
        rowVersion: this.data.bed?.rowVersion ?? 0,
      })
      .subscribe({
        next: (saved) => {
          this.notifications.success(`Bed ${saved.bedNumber} saved.`);
          this.dialogRef.close(saved);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(applyServerErrors(this.form, err) ?? apiErrorMessage(err));
        },
      });
  }
}
