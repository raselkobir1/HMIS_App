import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { filter, startWith } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../administration-api.service';
import { MONTHS, NumberSequence, ResetPolicy, SystemSettings, YearFormat } from '../administration.models';

const YEAR_FORMAT_LABELS: Record<YearFormat, string> = { None: 'No year', TwoDigit: 'Year (YY)', FourDigit: 'Year (YYYY)' };

@Component({
  selector: 'app-system-settings-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    PageHeader,
    FieldError,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Settings & Numbering" subtitle="Currency, financial year and document number formats" />

    <mat-card appearance="outlined" class="section">
      <mat-card-header><mat-card-title>General settings</mat-card-title></mat-card-header>
      <mat-card-content>
        <form [formGroup]="form" (ngSubmit)="save()" novalidate class="settings">
          <mat-form-field>
            <mat-label>Timezone</mat-label>
            <input matInput [value]="settings()?.timeZone ?? ''" readonly />
            <mat-hint>Fixed for this hospital</mat-hint>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Currency code</mat-label>
            <input matInput formControlName="currencyCode" maxlength="3" class="upper" />
            <mat-error><app-field-error [control]="form.controls.currencyCode" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Currency symbol</mat-label>
            <input matInput formControlName="currencySymbol" maxlength="5" />
            <mat-error><app-field-error [control]="form.controls.currencySymbol" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Financial year starts in</mat-label>
            <mat-select formControlName="financialYearStartMonth">
              @for (m of months; track m; let i = $index) {
                <mat-option [value]="i + 1">{{ m }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <div class="actions">
            <button mat-flat-button type="submit" [disabled]="saving() || form.pristine">Save settings</button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined" class="section">
      <mat-card-header>
        <mat-card-title>Number formats</mat-card-title>
        <mat-card-subtitle>Numbers are generated automatically and never reused</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <div class="table-scroll">
          <table mat-table [dataSource]="sequences()">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Document</th>
              <td mat-cell *matCellDef="let s">{{ s.name }}</td>
            </ng-container>
            <ng-container matColumnDef="format">
              <th mat-header-cell *matHeaderCellDef>Format</th>
              <td mat-cell *matCellDef="let s">
                {{ s.prefix || '—' }} + {{ yearLabel(s.yearFormat) }} + {{ s.padLength }} digits
                @if (s.resetPolicy === 'Yearly') {
                  <span class="badge info">Resets yearly</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="next">
              <th mat-header-cell *matHeaderCellDef>Next number</th>
              <td mat-cell *matCellDef="let s"><code>{{ s.nextNumberPreview }}</code></td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef class="actions-col"></th>
              <td mat-cell *matCellDef="let s" class="actions-col">
                <button mat-icon-button type="button" [attr.aria-label]="'Edit ' + s.name" (click)="edit(s)">
                  <mat-icon>edit</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    .section {
      margin-bottom: 16px;
    }
    .settings {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 4px 12px;
      padding-top: 8px;
    }
    .actions {
      grid-column: 1 / -1;
      display: flex;
      justify-content: flex-end;
    }
    .upper {
      text-transform: uppercase;
    }
    table {
      min-width: 560px;
    }
  `,
})
export class SystemSettingsPage {
  private readonly api = inject(AdministrationApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly months = MONTHS;
  protected readonly columns = ['name', 'format', 'next', 'actions'];
  protected readonly settings = signal<SystemSettings | null>(null);
  protected readonly sequences = signal<NumberSequence[]>([]);
  protected readonly saving = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    currencyCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    currencySymbol: ['', [Validators.required, Validators.maxLength(5)]],
    financialYearStartMonth: [7, Validators.required],
  });

  constructor() {
    this.api.getSettings().subscribe((s) => this.setSettings(s));
    this.loadSequences();
  }

  protected yearLabel(format: YearFormat): string {
    return YEAR_FORMAT_LABELS[format];
  }

  protected save(): void {
    const current = this.settings();
    if (!current || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.api.updateSettings({ ...this.form.getRawValue(), rowVersion: current.rowVersion }).subscribe({
      next: (s) => {
        this.setSettings(s);
        this.saving.set(false);
        this.notifications.success('Settings saved.');
      },
      error: (error) => {
        this.saving.set(false);
        this.notifications.error(applyServerErrors(this.form, error) ?? 'Please correct the highlighted fields.');
      },
    });
  }

  protected edit(sequence: NumberSequence): void {
    this.dialog
      .open(SequenceEditDialog, { data: sequence, width: '520px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.loadSequences());
  }

  private loadSequences(): void {
    this.api.getSequences().subscribe({
      next: (s) => this.sequences.set(s),
      error: (error) => this.notifications.error(apiErrorMessage(error, 'Number formats could not be loaded.')),
    });
  }

  private setSettings(s: SystemSettings): void {
    this.settings.set(s);
    this.form.reset({ currencyCode: s.currencyCode, currencySymbol: s.currencySymbol, financialYearStartMonth: s.financialYearStartMonth });
  }
}

/** Edits a number format with a live preview, and optionally moves the counter forward. */
@Component({
  selector: 'app-sequence-edit-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ sequence.name }}</h2>
    <mat-dialog-content>
      @if (bannerError(); as message) {
        <div class="banner" role="alert">{{ message }}</div>
      }
      <form [formGroup]="form" class="form-grid" novalidate>
        <mat-form-field>
          <mat-label>Prefix</mat-label>
          <input matInput formControlName="prefix" maxlength="10" />
          <mat-error><app-field-error [control]="form.controls.prefix" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Year in number</mat-label>
          <mat-select formControlName="yearFormat">
            <mat-option value="None">No year</mat-option>
            <mat-option value="TwoDigit">Two digits (YY)</mat-option>
            <mat-option value="FourDigit">Four digits (YYYY)</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Number of digits</mat-label>
          <input matInput type="number" formControlName="padLength" min="3" max="10" />
          <mat-error><app-field-error [control]="form.controls.padLength" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Counter</mat-label>
          <mat-select formControlName="resetPolicy">
            <mat-option value="Never">Never resets</mat-option>
            <mat-option value="Yearly">Resets every year</mat-option>
          </mat-select>
        </mat-form-field>
      </form>
      <p>Example: <code>{{ preview() }}</code></p>

      <h3 class="subhead">Continue from a previous system</h3>
      <p class="hint">Current next number: <code>{{ sequence.nextNumberPreview }}</code>. The counter can only move forward.</p>
      <div class="next-row">
        <mat-form-field>
          <mat-label>Next counter value</mat-label>
          <input matInput type="number" [formControl]="nextValue" min="1" />
        </mat-form-field>
        <button mat-stroked-button type="button" [disabled]="saving() || !nextValue.value" (click)="saveNextValue()">Set</button>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
      <button mat-flat-button type="button" [disabled]="saving() || form.pristine" (click)="saveFormat()">Save format</button>
    </mat-dialog-actions>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `
    .subhead {
      font: var(--mat-sys-title-small);
      margin: 16px 0 4px;
    }
    .next-row {
      display: flex;
      gap: 8px;
      align-items: baseline;
    }
  `,
})
export class SequenceEditDialog {
  protected readonly sequence = inject<NumberSequence>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SequenceEditDialog, boolean>);
  private readonly api = inject(AdministrationApiService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    prefix: [this.sequence.prefix, [Validators.maxLength(10), Validators.pattern(/^[A-Za-z0-9-]*$/)]],
    yearFormat: [this.sequence.yearFormat],
    padLength: [this.sequence.padLength, [Validators.required, Validators.min(3), Validators.max(10)]],
    resetPolicy: [this.sequence.resetPolicy as ResetPolicy],
  });
  protected readonly nextValue = this.fb.control<number | null>(null);

  private readonly formValue = toSignal(this.form.valueChanges.pipe(startWith(this.form.getRawValue())), {
    initialValue: this.form.getRawValue(),
  });

  protected readonly preview = computed(() => {
    const v = { ...this.form.getRawValue(), ...this.formValue() };
    const year = new Date().getFullYear();
    const yearPart = v.yearFormat === 'TwoDigit' ? String(year % 100).padStart(2, '0') : v.yearFormat === 'FourDigit' ? String(year) : '';
    return `${(v.prefix ?? '').toUpperCase()}${yearPart}${'1'.padStart(Number(v.padLength) || 0, '0')}`;
  });

  protected saveFormat(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    this.api.updateSequence(this.sequence.key, { ...this.form.getRawValue(), rowVersion: this.sequence.rowVersion }).subscribe({
      next: () => {
        this.notifications.success('Number format saved.');
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }

  protected saveNextValue(): void {
    const value = this.nextValue.value;
    if (!value) {
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    this.api.setSequenceNextValue(this.sequence.key, value).subscribe({
      next: (updated) => {
        this.notifications.success(`Next number is now ${updated.nextNumberPreview}.`);
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(apiErrorMessage(error));
      },
    });
  }
}
