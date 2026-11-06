import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ClinicalPermissions } from '../../clinical/clinical.models';
import { ageLabel } from '../../patient/patient.models';
import { Appointment, AppointmentApiService, formatTime, MyQueue, STATUS_LABELS } from '../appointment.models';

const REFRESH_MS = 30_000;

/** The signed-in doctor's queue for today. Route: /appointments/my-queue. */
@Component({
  selector: 'app-my-queue-page',
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="My queue" [subtitle]="queue() ? (queue()!.doctorName ?? '') + ' · ' + queue()!.date : 'Today'">
      <button mat-stroked-button type="button" (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
    </app-page-header>

    @if (loadError(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    } @else if (queue(); as q) {
      <div class="layout">
        <mat-card appearance="outlined" class="current">
          <mat-card-header><mat-card-title>Now seeing</mat-card-title></mat-card-header>
          <mat-card-content>
            @if (q.current; as c) {
              <div class="serial">{{ c.serialNumber }}</div>
              <div class="name"><a [routerLink]="['/patients', c.patientId]">{{ c.patientName }}</a></div>
              <div class="secondary">{{ c.patientMRNumber }} · {{ patientInfo(c) }} · {{ c.visitType === 'New' ? 'New' : 'Follow-up' }}</div>
              @if (c.notes) {
                <p class="notes">{{ c.notes }}</p>
              }
            } @else {
              <p class="secondary">No patient in consultation.</p>
            }
          </mat-card-content>
          <mat-card-actions>
            @if (q.current) {
              @if (charting) {
                <button mat-flat-button type="button" (click)="openVisit(q.current)"><mat-icon>edit_note</mat-icon> Open consultation</button>
                <button mat-stroked-button type="button" [disabled]="busy()" (click)="closeWithoutConsultation(q.current)">
                  <mat-icon>person_off</mat-icon> Close without consultation
                </button>
              } @else {
                <button mat-flat-button type="button" [disabled]="busy()" (click)="complete(q.current)"><mat-icon>check</mat-icon> Complete</button>
              }
            } @else {
              <button mat-flat-button type="button" [disabled]="busy() || q.waiting.length === 0" (click)="callNext()">
                <mat-icon>campaign</mat-icon> Call next
              </button>
            }
          </mat-card-actions>
        </mat-card>

        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Waiting ({{ q.waiting.length }})</mat-card-title></mat-card-header>
          <mat-card-content>
            <ol class="list">
              @for (a of q.waiting; track a.id) {
                <li>
                  <span class="chip">{{ a.serialNumber }}</span>
                  <span class="grow">
                    {{ a.patientName }}
                    <span class="secondary">· {{ patientInfo(a) }} · {{ a.visitType === 'New' ? 'New' : 'Follow-up' }}</span>
                  </span>
                  <button mat-button type="button" [disabled]="busy() || !!q.current" (click)="start(a)">See now</button>
                </li>
              } @empty {
                <li class="secondary">Nobody is waiting.</li>
              }
            </ol>
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined" class="others">
          <mat-card-header><mat-card-title>Rest of today</mat-card-title></mat-card-header>
          <mat-card-content>
            <ol class="list">
              @for (a of q.others; track a.id) {
                <li>
                  <span class="chip muted">{{ a.serialNumber }}</span>
                  <span class="grow">{{ a.patientName }} <span class="secondary">· {{ formatTime(a.estimatedTime) }}</span></span>
                  <span class="secondary">{{ labels[a.status] }}</span>
                </li>
              } @empty {
                <li class="secondary">No other appointments.</li>
              }
            </ol>
          </mat-card-content>
        </mat-card>
      </div>
    } @else {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    .layout { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 16px; align-items: start; }
    .current .serial { font: var(--mat-sys-display-medium); color: var(--mat-sys-primary); }
    .current .name { font: var(--mat-sys-title-large); }
    .notes { margin-top: 8px; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .list { list-style: none; margin: 0; padding: 0; }
    .list li { display: flex; align-items: center; gap: 12px; padding: 8px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .list li:last-child { border-bottom: 0; }
    .grow { flex: 1; }
    .chip {
      min-width: 36px; padding: 4px 8px; border-radius: 8px; text-align: center; font: var(--mat-sys-title-small);
      background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container);
    }
    .chip.muted { background: var(--mat-sys-surface-container-highest); color: var(--mat-sys-on-surface-variant); }
    .banner { padding: 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
  `,
})
export class MyQueuePage {
  private readonly api = inject(AppointmentApiService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  /** Doctors who write consultations go to the consultation screen instead of completing from the queue. */
  protected readonly charting = inject(AuthService).hasPermission(ClinicalPermissions.consultationManage);

  protected readonly formatTime = formatTime;
  protected readonly labels = STATUS_LABELS;
  protected readonly queue = signal<MyQueue | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    this.load();
    const timer = setInterval(() => this.load(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected load(): void {
    this.api.myQueue().subscribe({
      next: (q) => {
        this.queue.set(q);
        this.loadError.set(null);
      },
      error: (err) => this.loadError.set(apiErrorMessage(err, 'Your queue could not be loaded.')),
    });
  }

  protected patientInfo(a: Appointment): string {
    const age = a.patientDateOfBirth ? ageLabel(a.patientDateOfBirth, false) : '';
    return [age, a.patientGender].filter(Boolean).join(' / ');
  }

  protected callNext(): void {
    this.act(this.api.callNext(), (a) => `Serial ${a.serialNumber} called.`, (a) => this.openVisit(a));
  }

  protected start(a: Appointment): void {
    if (this.charting) {
      // The consultation screen starts the visit (and checks that no other consultation is open).
      void this.router.navigate(['/opd/encounters', a.id], { queryParams: { start: 1 } });
      return;
    }
    this.act(this.api.action(a.id, 'start'), (x) => `Serial ${x.serialNumber} called.`);
  }

  protected openVisit(a: Appointment): void {
    if (this.charting) {
      void this.router.navigate(['/opd/encounters', a.id]);
    }
  }

  protected complete(a: Appointment): void {
    this.act(this.api.action(a.id, 'complete'), (x) => `Serial ${x.serialNumber} completed.`);
  }

  /** Releases a patient who was called in by mistake or left: the visit closes with no consultation record. */
  protected closeWithoutConsultation(a: Appointment): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: 'Close without consultation?',
          message: `Serial ${a.serialNumber} — ${a.patientName} will be marked as completed with no consultation record. Use this when the patient was called by mistake or has left.`,
          confirmText: 'Close visit',
          destructive: true,
        },
      })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.complete(a));
  }

  private act(
    request$: ReturnType<AppointmentApiService['callNext']>,
    message: (a: Appointment) => string,
    then?: (a: Appointment) => void,
  ): void {
    this.busy.set(true);
    request$.subscribe({
      next: (a) => {
        this.busy.set(false);
        this.notifications.success(message(a));
        this.load();
        then?.(a);
      },
      error: (err) => {
        this.busy.set(false);
        this.notifications.error(apiErrorMessage(err));
        this.load();
      },
    });
  }
}
