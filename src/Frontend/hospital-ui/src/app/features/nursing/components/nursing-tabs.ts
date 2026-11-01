import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { filter, Observable, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ReasonDialog } from '../../billing/components/reason-dialog';
import {
  DOSE_STATE_LABELS,
  DoseSlot,
  FluidDay,
  Mar,
  MarOrder,
  NURSING_NOTE_LABELS,
  NursingApiService,
  NursingNote,
  NursingPermissions,
  NursingPatient,
} from '../nursing.models';
import { AdministerDialog, FluidDialog, NursingNoteDialog } from './nursing-dialogs';

const EARLY_LIMIT_MS = 2 * 60 * 60 * 1000;

const shiftDate = (date: string, days: number) => {
  const d = fromIsoDate(date)!;
  d.setDate(d.getDate() + days);
  return toIsoDate(d)!;
};

const patientLabel = (p: NursingPatient) => `${p.patientName} · ${p.mrNumber} · ${p.wardName} bed ${p.bedNumber}`;

/** Asks for a reason, then calls the correction endpoint and reloads. */
function markInError(dialog: MatDialog, notifications: NotificationService, call: (reason: string) => Observable<unknown>, done: () => void): void {
  dialog
    .open(ReasonDialog, {
      data: { title: 'Mark as entered in error?', message: 'The entry stays on the record, struck through.', label: 'Reason', confirmText: 'Mark in error', required: true, destructive: true },
    })
    .afterClosed()
    .pipe(
      filter((reason): reason is string => !!reason),
      switchMap(call),
    )
    .subscribe({ next: done, error: (err) => notifications.error(apiErrorMessage(err)) });
}

/** Medication Administration Record for one day: scheduled doses as chips, PRN doses on demand. */
@Component({
  selector: 'app-chart-mar-tab',
  imports: [DatePipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <div class="section-head">
        <h2>Medication administration</h2>
        <div class="toolbar">
          <button mat-icon-button type="button" (click)="go(-1)" aria-label="Previous day"><mat-icon>chevron_left</mat-icon></button>
          <span>{{ mar()?.date | date: 'EEE d MMM y' }}</span>
          <button mat-icon-button type="button" (click)="go(1)" aria-label="Next day"><mat-icon>chevron_right</mat-icon></button>
          <button mat-button type="button" (click)="load(null)">Today</button>
        </div>
      </div>
      @if (error(); as message) {
        <div class="banner error" role="alert">{{ message }}</div>
      }
      @for (o of mar()?.orders ?? []; track o.id) {
        <div class="row" [class.inactive]="o.status !== 'Active'">
          <div class="grow">
            <strong>{{ o.medicineName }}{{ o.strength ? ' ' + o.strength : '' }}</strong>
            <span class="badge" [class]="'badge ' + o.status">{{ o.status }}</span>
            <div>{{ o.dose }} · {{ o.route }} · {{ o.frequency }} <span class="secondary">· {{ o.orderedByName }}</span></div>
            @if (o.prnIndication) {
              <div class="secondary">Give when: {{ o.prnIndication }}</div>
            }
            @if (o.instructions) {
              <div class="secondary">{{ o.instructions }}</div>
            }
            @if (o.slots.length) {
              <div class="slots">
                @for (s of o.slots; track s.scheduledAt) {
                  <button type="button" class="slot" [class]="'slot ' + s.state" [disabled]="!canGive(o, s)" (click)="give(o, s)"
                          [attr.aria-label]="o.medicineName + ' ' + (s.scheduledAt | date: 'h:mm a') + ' ' + stateLabels[s.state]">
                    <span class="time">{{ s.scheduledAt | date: 'h:mm a' }}</span>
                    <span>{{ stateLabels[s.state] }}{{ s.entry ? ' · ' + (s.entry.occurredAt | date: 'h:mm a') : '' }}</span>
                  </button>
                }
              </div>
            }
            @if (o.entries.length) {
              <ul class="entries">
                @for (e of o.entries; track e.id) {
                  <li [class.error]="e.isEnteredInError">
                    {{ e.scheduledAt ? 'Dose ' + (e.scheduledAt | date: 'h:mm a') : 'PRN' }}: {{ e.status }}
                    {{ e.doseGiven ?? '' }} at {{ e.occurredAt | date: 'h:mm a' }}{{ e.reason ? ' — ' + e.reason : '' }}{{ e.notes ? ' · ' + e.notes : '' }}
                    · {{ e.recordedBy }}{{ e.isEnteredInError ? ' · in error: ' + e.errorReason : '' }}
                    @if (e.isMine && !e.isEnteredInError && canRecord()) {
                      <button mat-button type="button" class="small" (click)="entryInError(e.id)">Entered in error</button>
                    }
                  </li>
                }
              </ul>
            }
          </div>
          @if (o.frequency === 'PRN' && o.status === 'Active' && canRecord()) {
            <button mat-stroked-button type="button" (click)="give(o, null)">Give PRN</button>
          }
        </div>
      } @empty {
        <p class="secondary">{{ mar() ? 'No medication orders for this day.' : 'Loading…' }}</p>
      }
    </section>
  `,
  styleUrl: './chart-tab.scss',
  styles: `.banner.error { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }`,
})
export class ChartMarTab implements OnInit {
  readonly admissionId = input.required<string>();

  private readonly api = inject(NursingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly stateLabels = DOSE_STATE_LABELS;
  protected readonly mar = signal<Mar | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly canRecord = computed(() => !!this.mar()?.patient.isActive && this.auth.hasPermission(NursingPermissions.record));

  ngOnInit(): void {
    this.load(null);
  }

  protected load(date: string | null): void {
    this.api.mar(this.admissionId(), date).subscribe({
      next: (mar) => {
        this.error.set(null);
        this.mar.set(mar);
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'The MAR could not be loaded.')),
    });
  }

  protected go(days: number): void {
    const current = this.mar()?.date;
    if (current) {
      this.load(shiftDate(current, days));
    }
  }

  /** Unrecorded doses can be recorded up to 2 hours early; overdue ones late (documentation). */
  protected canGive(o: MarOrder, s: DoseSlot): boolean {
    return this.canRecord() && !s.entry && s.state !== 'Cancelled' && Date.parse(s.scheduledAt) - Date.now() <= EARLY_LIMIT_MS;
  }

  protected give(o: MarOrder, slot: DoseSlot | null): void {
    const mar = this.mar()!;
    this.dialog
      .open(AdministerDialog, {
        data: { admissionId: this.admissionId(), patientLabel: patientLabel(mar.patient), allergies: mar.patient.allergies, order: o, slot },
        width: '560px',
      })
      .afterClosed()
      .subscribe(() => this.load(mar.date));
  }

  protected entryInError(id: string): void {
    markInError(this.dialog, this.notifications, (reason) => this.api.doseInError(id, reason), () => this.load(this.mar()!.date));
  }
}

/** Intake/output for one fluid day (8am to 8am), with shift and 24-hour balance. */
@Component({
  selector: 'app-chart-fluids-tab',
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <div class="section-head">
        <h2>Intake / output</h2>
        <div class="toolbar">
          <button mat-icon-button type="button" (click)="go(-1)" aria-label="Previous day"><mat-icon>chevron_left</mat-icon></button>
          <span>{{ day()?.from | date: 'd MMM, h a' }} – {{ day()?.to | date: 'd MMM, h a' }}</span>
          <button mat-icon-button type="button" (click)="go(1)" aria-label="Next day"><mat-icon>chevron_right</mat-icon></button>
          <button mat-button type="button" (click)="load(null)">Current</button>
          @if (canRecord()) {
            <button mat-flat-button type="button" (click)="record()"><mat-icon>water_drop</mat-icon> Record</button>
          }
        </div>
      </div>
      @if (day(); as d) {
        <div class="totals">
          @for (t of d.byShift; track t.label) {
            <div class="total">
              <span class="secondary">{{ t.label }}</span>
              <strong>{{ t.intake | number }} / {{ t.output | number }} ml</strong>
              <span class="secondary">in / out</span>
            </div>
          }
          <div class="total day">
            <span class="secondary">24 hours</span>
            <strong [class.negative]="d.total.balance < 0">{{ d.total.balance > 0 ? '+' : '' }}{{ d.total.balance | number }} ml</strong>
            <span class="secondary">{{ d.total.intake | number }} in · {{ d.total.output | number }} out</span>
          </div>
        </div>
        <div class="table-scroll">
          <table class="grid">
            <thead><tr><th>Time</th><th>In / out</th><th>Type</th><th>ml</th><th>Notes</th><th>By</th><th></th></tr></thead>
            <tbody>
              @for (e of d.entries; track e.id) {
                <tr [class.error]="e.isEnteredInError">
                  <td>{{ e.measuredAt | date: 'h:mm a' }}</td>
                  <td>{{ e.direction }}</td>
                  <td>{{ e.category }}</td>
                  <td>{{ e.volumeMl | number }}</td>
                  <td>{{ e.notes }}{{ e.isEnteredInError ? ' · in error: ' + e.errorReason : '' }}</td>
                  <td>{{ e.recordedBy }}</td>
                  <td>
                    @if (e.isMine && !e.isEnteredInError && canRecord()) {
                      <button mat-icon-button type="button" (click)="entryInError(e.id)" aria-label="Mark entry as entered in error"><mat-icon>block</mat-icon></button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="secondary">Nothing charted for this fluid day.</td></tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <p class="secondary">{{ error() ?? 'Loading…' }}</p>
      }
    </section>
  `,
  styleUrl: './chart-tab.scss',
})
export class ChartFluidsTab implements OnInit {
  readonly admissionId = input.required<string>();

  private readonly api = inject(NursingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly day = signal<FluidDay | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly canRecord = computed(() => !!this.day()?.patient.isActive && this.auth.hasPermission(NursingPermissions.record));

  ngOnInit(): void {
    this.load(null);
  }

  protected load(date: string | null): void {
    this.api.fluids(this.admissionId(), date).subscribe({
      next: (day) => this.day.set(day),
      error: (err) => this.error.set(apiErrorMessage(err, 'Intake/output could not be loaded.')),
    });
  }

  protected go(days: number): void {
    const current = this.day()?.date;
    if (current) {
      this.load(shiftDate(current, days));
    }
  }

  protected record(): void {
    const d = this.day()!;
    this.dialog
      .open(FluidDialog, { data: { admissionId: this.admissionId(), intakeCategories: d.intakeCategories, outputCategories: d.outputCategories }, width: '520px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.load(null));
  }

  protected entryInError(id: string): void {
    markInError(this.dialog, this.notifications, (reason) => this.api.fluidInError(id, reason), () => this.load(this.day()!.date));
  }
}

/** Nursing assessment, care plan, progress and incident notes, newest first. */
@Component({
  selector: 'app-chart-nursing-notes-tab',
  imports: [DatePipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <div class="section-head">
        <h2>Nursing notes</h2>
        @if (canRecord()) {
          <button mat-flat-button type="button" (click)="write()"><mat-icon>edit_note</mat-icon> Write note</button>
        }
      </div>
      @for (n of notes(); track n.id) {
        <article class="note" [class.error]="n.isEnteredInError">
          <header>
            <strong>{{ labels[n.kind] }}</strong> · {{ n.writtenBy }} · {{ n.writtenAt | date: 'd MMM y, h:mm a' }}
            @if (n.isEnteredInError) {
              <span class="badge Stopped">Entered in error: {{ n.errorReason }}</span>
            } @else if (n.isMine && canRecord()) {
              <button mat-button type="button" class="small" (click)="noteInError(n.id)">Entered in error</button>
            }
          </header>
          <p lang="bn">{{ n.text }}</p>
        </article>
      } @empty {
        <p class="secondary">{{ loaded() ? 'No nursing notes yet.' : 'Loading…' }}</p>
      }
    </section>
  `,
  styleUrl: './chart-tab.scss',
})
export class ChartNursingNotesTab implements OnInit {
  readonly admissionId = input.required<string>();

  private readonly api = inject(NursingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly labels = NURSING_NOTE_LABELS;
  protected readonly notes = signal<NursingNote[]>([]);
  protected readonly loaded = signal(false);
  private readonly active = signal(false);
  protected readonly canRecord = computed(() => this.active() && this.auth.hasPermission(NursingPermissions.record));

  ngOnInit(): void {
    this.load();
  }

  protected write(): void {
    this.dialog
      .open(NursingNoteDialog, { data: { admissionId: this.admissionId() }, width: '600px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }

  protected noteInError(id: string): void {
    markInError(this.dialog, this.notifications, (reason) => this.api.noteInError(id, reason), () => this.load());
  }

  private load(): void {
    this.api.notes(this.admissionId()).subscribe({
      next: (r) => {
        this.notes.set(r.notes);
        this.active.set(r.patient.isActive);
        this.loaded.set(true);
      },
      error: (err) => this.notifications.error(apiErrorMessage(err)),
    });
  }
}
