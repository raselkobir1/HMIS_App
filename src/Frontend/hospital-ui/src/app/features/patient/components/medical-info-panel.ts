import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { filter, Observable } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { PatientApiService } from '../patient-api.service';
import {
  Allergy,
  ALLERGY_TYPES,
  AllergySeverity,
  AllergyStatus,
  AllergyType,
  HISTORY_CATEGORIES,
  HistoryCategory,
  MedicalHistory,
  MedicalInfo,
  SEVERITIES,
} from '../patient.models';

/** Allergies and baseline history. Entries are never deleted; mistakes are marked "entered in error". */
@Component({
  selector: 'app-medical-info-panel',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatMenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <header class="section-head">
        <h3>Allergies</h3>
        @if (canManage()) {
          <button mat-stroked-button type="button" (click)="editAllergy(null)"><mat-icon>add</mat-icon> Add allergy</button>
        }
      </header>
      @for (a of info().allergies; track a.id) {
        <div class="row" [class.struck]="a.status === 'EnteredInError'">
          <span class="badge" [class.warn]="a.severity === 'Severe' && a.status === 'Active'"
                [class.info]="a.severity !== 'Severe' && a.status === 'Active'"
                [class.muted]="a.status !== 'Active'">{{ a.severity }}</span>
          <div class="grow">
            <strong>{{ a.allergen }}</strong> <span class="hint">({{ a.type }})</span>
            @if (a.reaction) {
              <div class="hint">Reaction: {{ a.reaction }}</div>
            }
            <div class="hint">{{ statusLabel(a.status) }} · recorded {{ a.createdAt | date: 'dd MMM yyyy' }}</div>
          </div>
          @if (canManage() && a.status !== 'EnteredInError') {
            <button mat-icon-button type="button" [matMenuTriggerFor]="menu" [attr.aria-label]="'Actions for ' + a.allergen">
              <mat-icon>more_vert</mat-icon>
            </button>
            <mat-menu #menu="matMenu" xPosition="before">
              <button mat-menu-item type="button" (click)="editAllergy(a)"><mat-icon>edit</mat-icon><span>Edit</span></button>
              @if (a.status === 'Active') {
                <button mat-menu-item type="button" (click)="setStatus(a, 'Resolved')"><mat-icon>check</mat-icon><span>Mark resolved</span></button>
              } @else {
                <button mat-menu-item type="button" (click)="setStatus(a, 'Active')"><mat-icon>undo</mat-icon><span>Mark active</span></button>
              }
              <button mat-menu-item type="button" (click)="setStatus(a, 'EnteredInError')">
                <mat-icon>block</mat-icon><span>Entered in error</span>
              </button>
            </mat-menu>
          }
        </div>
      } @empty {
        <p class="hint">No known allergies recorded.</p>
      }
    </section>

    <section>
      <header class="section-head">
        <h3>Medical history</h3>
        @if (canManage()) {
          <button mat-stroked-button type="button" (click)="editHistory(null)"><mat-icon>add</mat-icon> Add entry</button>
        }
      </header>
      @for (h of info().history; track h.id) {
        <div class="row" [class.struck]="h.isEnteredInError">
          <span class="badge muted">{{ categoryLabel(h.category) }}</span>
          <div class="grow">
            <div>{{ h.description }}</div>
            <div class="hint">
              @if (h.onsetDate) { Since {{ h.onsetDate }} · }
              recorded {{ h.createdAt | date: 'dd MMM yyyy' }}
              @if (h.isEnteredInError) { · entered in error }
            </div>
          </div>
          @if (canManage() && !h.isEnteredInError) {
            <button mat-icon-button type="button" [matMenuTriggerFor]="hmenu" aria-label="Entry actions">
              <mat-icon>more_vert</mat-icon>
            </button>
            <mat-menu #hmenu="matMenu" xPosition="before">
              <button mat-menu-item type="button" (click)="editHistory(h)"><mat-icon>edit</mat-icon><span>Edit</span></button>
              <button mat-menu-item type="button" (click)="markHistoryInError(h)"><mat-icon>block</mat-icon><span>Entered in error</span></button>
            </mat-menu>
          }
        </div>
      } @empty {
        <p class="hint">No medical history recorded.</p>
      }
    </section>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    section {
      margin-bottom: 24px;
    }
    .section-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    h3 {
      font: var(--mat-sys-title-medium);
      margin: 8px 0;
    }
    .row {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 8px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .grow {
      flex: 1;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .struck .grow > :first-child {
      text-decoration: line-through;
    }
  `,
})
export class MedicalInfoPanel {
  readonly patientId = input.required<string>();
  readonly info = input.required<MedicalInfo>();
  readonly canManage = input(false);
  readonly changed = output<void>();

  private readonly api = inject(PatientApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected statusLabel(status: AllergyStatus): string {
    return status === 'EnteredInError' ? 'Entered in error' : status;
  }

  protected categoryLabel(category: HistoryCategory): string {
    return HISTORY_CATEGORIES.find((c) => c.value === category)?.label ?? category;
  }

  protected editAllergy(allergy: Allergy | null): void {
    this.afterSave(this.dialog.open(AllergyDialog, { data: { patientId: this.patientId(), allergy }, width: '480px', maxWidth: '95vw' }).afterClosed());
  }

  protected editHistory(entry: MedicalHistory | null): void {
    this.afterSave(this.dialog.open(HistoryDialog, { data: { patientId: this.patientId(), entry }, width: '480px', maxWidth: '95vw' }).afterClosed());
  }

  protected setStatus(allergy: Allergy, status: AllergyStatus): void {
    this.api.setAllergyStatus(this.patientId(), allergy.id, status).subscribe({
      next: () => this.changed.emit(),
      error: (error) => this.notifications.error(apiErrorMessage(error)),
    });
  }

  protected markHistoryInError(entry: MedicalHistory): void {
    this.api.markHistoryInError(this.patientId(), entry.id).subscribe({
      next: () => this.changed.emit(),
      error: (error) => this.notifications.error(apiErrorMessage(error)),
    });
  }

  private afterSave(closed: Observable<boolean | undefined>): void {
    closed.pipe(filter(Boolean)).subscribe(() => this.changed.emit());
  }
}

@Component({
  selector: 'app-allergy-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.allergy ? 'Edit allergy' : 'Add allergy' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content class="form-grid">
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="span">
          <mat-label>Allergen</mat-label>
          <input matInput formControlName="allergen" placeholder="e.g. Penicillin" />
          <mat-error><app-field-error [control]="form.controls.allergen" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Type</mat-label>
          <mat-select formControlName="type">
            @for (t of types; track t) {
              <mat-option [value]="t">{{ t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Severity</mat-label>
          <mat-select formControlName="severity">
            @for (s of severities; track s) {
              <mat-option [value]="s">{{ s }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="span">
          <mat-label>Reaction</mat-label>
          <input matInput formControlName="reaction" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
  styles: `.span { grid-column: 1 / -1; }`,
})
export class AllergyDialog {
  protected readonly data = inject<{ patientId: string; allergy: Allergy | null }>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<AllergyDialog, boolean>);
  private readonly api = inject(PatientApiService);

  protected readonly types = ALLERGY_TYPES;
  protected readonly severities = SEVERITIES;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    allergen: [this.data.allergy?.allergen ?? '', [Validators.required, Validators.maxLength(150)]],
    type: [this.data.allergy?.type ?? ('Drug' as AllergyType)],
    severity: [this.data.allergy?.severity ?? ('Moderate' as AllergySeverity)],
    reaction: [this.data.allergy?.reaction ?? '', Validators.maxLength(500)],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const v = this.form.getRawValue();
    const body = { ...v, reaction: v.reaction.trim() || null };
    const request$ = this.data.allergy
      ? this.api.updateAllergy(this.data.patientId, this.data.allergy.id, { ...body, rowVersion: this.data.allergy.rowVersion })
      : this.api.addAllergy(this.data.patientId, body);
    request$.subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err));
      },
    });
  }
}

@Component({
  selector: 'app-history-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatButtonModule, FieldError],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.entry ? 'Edit history entry' : 'Add history entry' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Category</mat-label>
          <mat-select formControlName="category">
            @for (c of categories; track c.value) {
              <mat-option [value]="c.value">{{ c.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
          <mat-error><app-field-error [control]="form.controls.description" /></mat-error>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Since (optional)</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="onsetDate" [max]="today" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
})
export class HistoryDialog {
  protected readonly data = inject<{ patientId: string; entry: MedicalHistory | null }>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<HistoryDialog, boolean>);
  private readonly api = inject(PatientApiService);

  protected readonly categories = HISTORY_CATEGORIES;
  protected readonly today = new Date();
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    category: [this.data.entry?.category ?? ('PastIllness' as HistoryCategory)],
    description: [this.data.entry?.description ?? '', [Validators.required, Validators.maxLength(1000)]],
    onsetDate: [fromIsoDate(this.data.entry?.onsetDate) as Date | null],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const v = this.form.getRawValue();
    const body = { category: v.category, description: v.description.trim(), onsetDate: toIsoDate(v.onsetDate) };
    const request$ = this.data.entry
      ? this.api.updateHistory(this.data.patientId, this.data.entry.id, { ...body, rowVersion: this.data.entry.rowVersion })
      : this.api.addHistory(this.data.patientId, body);
    request$.subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.saving.set(false);
        this.error.set(applyServerErrors(this.form, err));
      },
    });
  }
}
