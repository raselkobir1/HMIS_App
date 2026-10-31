import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { filter, switchMap } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { PatientApiService } from '../patient-api.service';
import { DOCUMENT_CATEGORIES, DocumentCategory, PatientDocument } from '../patient.models';

@Component({
  selector: 'app-documents-panel',
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="section-head">
      <h3>Documents</h3>
      @if (canUpload()) {
        <button mat-stroked-button type="button" (click)="upload()"><mat-icon>upload_file</mat-icon> Upload</button>
      }
    </header>
    @if (error(); as message) {
      <p class="error">{{ message }}</p>
    }
    @for (d of documents(); track d.id) {
      <div class="row">
        <mat-icon>{{ d.contentType === 'application/pdf' ? 'picture_as_pdf' : 'image' }}</mat-icon>
        <div class="grow">
          <strong>{{ d.title }}</strong>
          <div class="hint">{{ categoryLabel(d.category) }} · {{ d.fileName }} · {{ d.sizeBytes / 1024 | number: '1.0-0' }} KB · {{ d.uploadedAt | date: 'dd MMM yyyy, h:mm a' }}</div>
        </div>
        <button mat-icon-button type="button" (click)="view(d)" [attr.aria-label]="'Open ' + d.title"><mat-icon>open_in_new</mat-icon></button>
        @if (canDelete()) {
          <button mat-icon-button type="button" (click)="remove(d)" [attr.aria-label]="'Remove ' + d.title"><mat-icon>delete</mat-icon></button>
        }
      </div>
    } @empty {
      <p class="hint">No documents uploaded.</p>
    }
  `,
  styles: `
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
      align-items: center;
      gap: 12px;
      padding: 8px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .grow {
      flex: 1;
      min-width: 0;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      overflow-wrap: anywhere;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class DocumentsPanel implements OnInit {
  readonly patientId = input.required<string>();
  readonly canUpload = input(false);
  readonly canDelete = input(false);

  private readonly api = inject(PatientApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly documents = signal<PatientDocument[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected categoryLabel(category: DocumentCategory): string {
    return DOCUMENT_CATEGORIES.find((c) => c.value === category)?.label ?? category;
  }

  protected upload(): void {
    this.dialog
      .open(DocumentUploadDialog, { data: this.patientId(), width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }

  /** Files are fetched with the session token and opened from a temporary local URL. */
  protected view(document: PatientDocument): void {
    const tab = window.open('', '_blank');
    this.api.documentContent(this.patientId(), document.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        if (tab) {
          tab.location.href = url;
        } else {
          window.open(url, '_blank');
        }
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: (err) => {
        tab?.close();
        this.notifications.error(apiErrorMessage(err, 'The document could not be opened.'));
      },
    });
  }

  protected remove(document: PatientDocument): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: 'Remove document',
          message: `Remove "${document.title}" from this patient? The file is kept in the audit trail.`,
          confirmText: 'Remove',
          destructive: true,
        },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.removeDocument(this.patientId(), document.id)),
      )
      .subscribe({
        next: () => {
          this.notifications.success('Document removed.');
          this.load();
        },
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }

  private load(): void {
    this.api.documents(this.patientId()).subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.error.set(null);
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'Documents could not be loaded.')),
    });
  }
}

@Component({
  selector: 'app-document-upload-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Upload document</h2>
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
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" placeholder="e.g. CBC report, Oct 2026" />
          <mat-error><app-field-error [control]="form.controls.title" /></mat-error>
        </mat-form-field>
        <input #fileInput type="file" accept="application/pdf,image/png,image/jpeg" hidden (change)="pick(fileInput)" />
        <button mat-stroked-button type="button" (click)="fileInput.click()"><mat-icon>attach_file</mat-icon> Choose file</button>
        <span class="hint"> {{ file()?.name ?? 'PDF, JPEG or PNG, up to 10 MB' }}</span>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving() || !file()">Upload</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
})
export class DocumentUploadDialog {
  private readonly patientId = inject<string>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DocumentUploadDialog, boolean>);
  private readonly api = inject(PatientApiService);

  protected readonly categories = DOCUMENT_CATEGORIES;
  protected readonly file = signal<File | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    category: ['LabReport' as DocumentCategory],
    title: ['', [Validators.required, Validators.maxLength(150)]],
  });

  protected pick(input: HTMLInputElement): void {
    const file = input.files?.[0] ?? null;
    if (file && file.size > 10 * 1024 * 1024) {
      this.error.set('The file is larger than 10 MB.');
      return;
    }
    this.error.set(null);
    this.file.set(file);
    if (file && !this.form.controls.title.value) {
      this.form.controls.title.setValue(file.name.replace(/\.[^.]+$/, ''));
    }
  }

  protected save(): void {
    const file = this.file();
    if (this.form.invalid || !file) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const { category, title } = this.form.getRawValue();
    this.api.uploadDocument(this.patientId, file, category, title.trim()).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'The document could not be uploaded.'));
      },
    });
  }
}
