import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { filter } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { fromIsoDate, toIsoDate, todayIso } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { CurrentShift, Handover, NursingApiService, NursingPermissions, Shift, SHIFT_LABELS, WardPatient } from '../nursing.models';

interface HandoverDialogData {
  wardId: string;
  wardName: string;
  date: string;
  shift: Shift;
  patients: WardPatient[];
  existing: Handover | null;
}

/** Writes or revises the outgoing shift's handover: a ward summary and a note per patient (blank notes are left out). */
@Component({
  selector: 'app-handover-dialog',
  imports: [DatePipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Handover · {{ data.wardName }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        <p class="hint">{{ shiftLabels[data.shift] }} shift · {{ data.date | date: 'EEE d MMM' }}. Blank patient notes are left out.</p>
        @if (error(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Ward summary</mat-label>
          <textarea matInput formControlName="summary" rows="3" maxlength="4000" placeholder="Admissions, discharges, pending tasks, equipment…"></textarea>
        </mat-form-field>
        <div formArrayName="items">
          @for (p of data.patients; track p.admissionId; let i = $index) {
            <mat-form-field class="full">
              <mat-label>{{ p.bedNumber }} · {{ p.patientName }}</mat-label>
              <textarea matInput [formControlName]="i" rows="2" maxlength="1000"></textarea>
            </mat-form-field>
          }
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save handover</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
})
export class HandoverDialog {
  protected readonly data = inject<HandoverDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<HandoverDialog, Handover>);
  private readonly api = inject(NursingApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly shiftLabels = SHIFT_LABELS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new FormGroup({
    summary: new FormControl(this.data.existing?.summary ?? '', { nonNullable: true, validators: Validators.maxLength(4000) }),
    items: new FormArray(
      this.data.patients.map(
        (p) => new FormControl(this.data.existing?.items.find((i) => i.admissionId === p.admissionId)?.note ?? '', { nonNullable: true }),
      ),
    ),
  });

  protected save(): void {
    const v = this.form.getRawValue();
    const items = this.data.patients
      .map((p, i) => ({ admissionId: p.admissionId, note: v.items[i].trim() }))
      .filter((i) => i.note);
    if (!v.summary.trim() && !items.length) {
      this.error.set('Write a ward summary or a note for at least one patient.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api
      .saveHandover({
        wardId: this.data.wardId,
        date: this.data.date,
        shift: this.data.shift,
        summary: v.summary.trim() || null,
        items,
        rowVersion: this.data.existing?.rowVersion ?? 0,
      })
      .subscribe({
        next: (h) => {
          this.notifications.success('Handover saved.');
          this.dialogRef.close(h);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err));
        },
      });
  }
}

/** Shift handovers per ward: the outgoing nurse writes, the incoming nurse receives. Route: /nursing/handover. */
@Component({
  selector: 'app-handover-page',
  imports: [DatePipe, FormsModule, MatFormFieldModule, MatSelectModule, MatButtonModule, MatIconModule, MatCardModule, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Shift Handover" subtitle="What the next shift needs to know, ward by ward">
      @if (canRecord && shift() && wardId()) {
        <button mat-flat-button type="button" (click)="write()" [disabled]="!canWriteCurrent()">
          <mat-icon>edit_note</mat-icon> {{ current() ? 'Revise my handover' : 'Write handover' }}
        </button>
      }
    </app-page-header>
    @if (shift(); as s) {
      <section class="filters">
        <mat-form-field>
          <mat-label>Ward</mat-label>
          <mat-select [ngModel]="wardId()" (ngModelChange)="wardId.set($event); load()">
            @for (w of s.allWards; track w.id) {
              <mat-option [value]="w.id">{{ w.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Day</mat-label>
          <mat-select [ngModel]="date()" (ngModelChange)="date.set($event); load()">
            <mat-option [value]="today">Today</mat-option>
            <mat-option [value]="yesterday">Yesterday</mat-option>
          </mat-select>
        </mat-form-field>
        <span class="secondary">Now: {{ shiftLabels[s.shift] }} shift of {{ s.date | date: 'd MMM' }}</span>
      </section>
    }
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    @for (h of handovers(); track h.id) {
      <mat-card appearance="outlined">
        <mat-card-header>
          <mat-card-title>{{ shiftLabels[h.shift] }} · {{ h.date | date: 'EEE d MMM' }}</mat-card-title>
          <mat-card-subtitle>
            Written by {{ h.writtenBy }} at {{ h.writtenAt | date: 'h:mm a' }} ·
            @if (h.receivedAt) {
              received by {{ h.receivedBy }} at {{ h.receivedAt | date: 'h:mm a' }}
            } @else {
              <strong>not yet received</strong>
            }
          </mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          @if (h.summary) {
            <p class="pre">{{ h.summary }}</p>
          }
          @for (i of h.items; track i.admissionId) {
            <div class="item"><span class="bed">{{ i.bedNumber }}</span><span><strong>{{ i.patientName }}</strong> {{ i.note }}</span></div>
          }
        </mat-card-content>
        @if (canRecord && !h.receivedAt && !h.isMine) {
          <mat-card-actions align="end">
            <button mat-flat-button type="button" (click)="receive(h)"><mat-icon>task_alt</mat-icon> Receive handover</button>
          </mat-card-actions>
        }
      </mat-card>
    } @empty {
      <p class="secondary">{{ loaded() ? 'No handovers for this ward and day.' : 'Loading…' }}</p>
    }
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .pre { white-space: pre-wrap; }
    .item { display: flex; gap: 10px; padding: 6px 0; border-top: 1px solid var(--mat-sys-outline-variant); white-space: pre-wrap; }
    .bed { min-width: 44px; font-weight: 600; }
    mat-card-content { padding-top: 8px; }
  `,
})
export class HandoverPage implements OnInit {
  private readonly api = inject(NursingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly canRecord = inject(AuthService).hasPermission(NursingPermissions.record);
  protected readonly shiftLabels = SHIFT_LABELS;
  protected readonly today = todayIso();
  protected readonly yesterday = toIsoDate(new Date(Date.now() - 86_400_000))!;
  protected readonly shift = signal<CurrentShift | null>(null);
  protected readonly wardId = signal<string | null>(null);
  protected readonly date = signal(this.today);
  protected readonly handovers = signal<Handover[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);

  /** The handover of the shift in progress for the selected ward, if already written. */
  protected readonly current = computed(() => {
    const s = this.shift();
    return this.handovers().find((h) => s && h.date === s.date && h.shift === s.shift) ?? null;
  });
  protected readonly canWriteCurrent = computed(() => {
    const h = this.current();
    return !h || (h.isMine && !h.receivedAt);
  });

  ngOnInit(): void {
    this.api.currentShift().subscribe({
      next: (s) => {
        this.shift.set(s);
        const id = s.myWards[0]?.id ?? s.allWards[0]?.id ?? null;
        this.wardId.set(id);
        // The night shift started yesterday until 8am: show that day.
        this.date.set(s.date === this.today ? this.today : this.yesterday);
        this.load();
      },
      error: (err) => this.error.set(apiErrorMessage(err)),
    });
  }

  protected load(): void {
    const id = this.wardId();
    if (!id) {
      this.loaded.set(true);
      return;
    }
    this.api.handovers(id, this.date()).subscribe({
      next: (list) => {
        // The API also returns the day before (for the overnight shift); keep the chosen day and its preceding night.
        const prev = toIsoDate(new Date(fromIsoDate(this.date())!.getTime() - 86_400_000));
        this.handovers.set(list.filter((h) => h.date === this.date() || (h.date === prev && h.shift === 'Night')));
        this.loaded.set(true);
        this.error.set(null);
      },
      error: (err) => this.error.set(apiErrorMessage(err)),
    });
  }

  protected write(): void {
    const s = this.shift()!;
    const wardId = this.wardId()!;
    this.api.ward(wardId).subscribe({
      next: (ward) =>
        this.dialog
          .open(HandoverDialog, {
            data: { wardId, wardName: ward.ward.name, date: s.date, shift: s.shift, patients: ward.patients, existing: this.current() },
            width: '680px',
          })
          .afterClosed()
          .pipe(filter(Boolean))
          .subscribe(() => {
            this.date.set(s.date);
            this.load();
          }),
      error: (err) => this.notifications.error(apiErrorMessage(err)),
    });
  }

  protected receive(h: Handover): void {
    this.api.receiveHandover(h.id, h.rowVersion).subscribe({
      next: () => {
        this.notifications.success('Handover received.');
        this.load();
      },
      error: (err) => {
        this.notifications.error(apiErrorMessage(err));
        this.load();
      },
    });
  }
}
