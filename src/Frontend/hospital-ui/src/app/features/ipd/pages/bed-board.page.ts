import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink } from '@angular/router';
import { filter, Observable, of, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ReasonDialog } from '../../billing/components/reason-dialog';
import { TakaPipe } from '../../billing/components/taka.pipe';
import { BED_STATUS_LABELS, BedStatus, BoardBed, IpdApiService, IpdPermissions, WARD_TYPE_LABELS, WardBoard } from '../ipd.models';

const REFRESH_MS = 60_000;

/** All active wards and beds by status; occupied beds show the patient. Route: /ipd/beds. */
@Component({
  selector: 'app-bed-board-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatMenuModule, PageHeader, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Bed board" [subtitle]="summary()">
      <button mat-stroked-button type="button" (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      @if (can.admit) {
        <a mat-flat-button routerLink="/ipd/admit"><mat-icon>add</mat-icon> Admit patient</a>
      }
    </app-page-header>

    <div class="legend" aria-hidden="true">
      @for (s of statuses; track s) {
        <span class="dot" [class]="'dot ' + s"></span>{{ labels[s] }}
      }
    </div>

    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    @for (w of board(); track w.id) {
      <section class="ward" [attr.aria-label]="w.name">
        <header>
          <h2>{{ w.name }}</h2>
          <span class="secondary">{{ wardTypes[w.type] }}{{ w.building ? ' · ' + w.building : '' }}{{ w.floor ? ', floor ' + w.floor : '' }}</span>
          <span class="counts">{{ w.available }} free · {{ w.occupied }} occupied{{ w.other ? ' · ' + w.other + ' not ready' : '' }}</span>
        </header>
        <div class="beds">
          @for (b of w.beds; track b.id) {
            <article class="bed" [class]="'bed ' + b.status">
              <div class="top">
                <strong>{{ b.bedNumber }}</strong>
                @if (b.room) {
                  <span class="secondary">Room {{ b.room }}</span>
                }
                @if (can.housekeeping && b.status !== 'Occupied') {
                  <button mat-icon-button type="button" class="menu" [matMenuTriggerFor]="menu" [attr.aria-label]="'Change status of bed ' + b.bedNumber">
                    <mat-icon>more_vert</mat-icon>
                  </button>
                  <mat-menu #menu="matMenu">
                    @for (s of settable; track s) {
                      @if (s !== b.status) {
                        <button mat-menu-item type="button" (click)="setStatus(b, s)">{{ labels[s] }}</button>
                      }
                    }
                  </mat-menu>
                }
              </div>
              @if (b.occupant; as o) {
                <a class="occupant" [routerLink]="['/ipd/admissions', o.admissionId]">
                  <span class="name">{{ o.patientName }}</span>
                  <span class="secondary">{{ o.patientMRNumber }} · {{ o.patientGender }}</span>
                  <span class="secondary">{{ o.doctorName }} · since {{ o.admittedAt | date: 'd MMM' }}</span>
                  @if (o.status !== 'Admitted') {
                    <span class="flag">{{ o.status === 'DischargeOrdered' ? 'Discharge ordered' : 'Final bill ready' }}</span>
                  }
                </a>
              } @else {
                <div class="state">{{ labels[b.status] }}{{ b.statusNote ? ' — ' + b.statusNote : '' }}</div>
                <div class="secondary">{{ b.categoryName }} · {{ b.dailyRate | taka }}/day</div>
                @if (can.admit && (b.status === 'Available' || b.status === 'Reserved')) {
                  <a mat-button routerLink="/ipd/admit" [queryParams]="{ bedId: b.id }" class="admit">Admit here</a>
                }
              }
            </article>
          } @empty {
            <p class="secondary">No beds in this ward.</p>
          }
        </div>
      </section>
    } @empty {
      @if (loaded()) {
        <p class="secondary">No wards yet. An administrator sets up wards and beds under IPD → Ward & Bed Setup.</p>
      }
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 16px; }
    .legend { display: flex; flex-wrap: wrap; gap: 4px 14px; align-items: center; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 4px; }
    .banner { padding: 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .ward header { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 12px; margin-bottom: 8px; }
    h2 { margin: 0; font: var(--mat-sys-title-large); }
    .counts { margin-left: auto; font: var(--mat-sys-label-large); }
    .beds { display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 10px; }
    .bed { display: flex; flex-direction: column; gap: 2px; min-height: 108px; padding: 8px 10px; border-radius: 10px;
           border: 1px solid var(--mat-sys-outline-variant); border-left-width: 6px; background: var(--mat-sys-surface); }
    .top { display: flex; align-items: center; gap: 8px; }
    .menu { margin: -8px -8px -8px auto; }
    .occupant { display: flex; flex-direction: column; color: inherit; text-decoration: none; }
    .occupant:hover .name { text-decoration: underline; }
    .name { font: var(--mat-sys-title-small); }
    .flag { align-self: flex-start; margin-top: 4px; padding: 1px 8px; border-radius: 999px; font: var(--mat-sys-label-small);
            background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .state { font: var(--mat-sys-label-large); }
    .admit { align-self: flex-start; margin: 2px 0 0 -12px; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    /* Status colours (with text labels, never colour alone). */
    .Available { border-left-color: #2e7d32; } .dot.Available { background: #2e7d32; }
    .Occupied { border-left-color: var(--mat-sys-primary); } .dot.Occupied { background: var(--mat-sys-primary); }
    .Reserved { border-left-color: #8e24aa; } .dot.Reserved { background: #8e24aa; }
    .UnderMaintenance { border-left-color: #757575; } .dot.UnderMaintenance { background: #757575; }
    .CleaningRequired { border-left-color: #ef6c00; } .dot.CleaningRequired { background: #ef6c00; }
  `,
})
export class BedBoardPage {
  private readonly api = inject(IpdApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly can = {
    admit: this.auth.hasPermission(IpdPermissions.admit),
    housekeeping: this.auth.hasPermission(IpdPermissions.housekeeping),
  };
  protected readonly labels = BED_STATUS_LABELS;
  protected readonly wardTypes = WARD_TYPE_LABELS;
  protected readonly statuses = Object.keys(BED_STATUS_LABELS) as BedStatus[];
  protected readonly settable: BedStatus[] = ['Available', 'CleaningRequired', 'UnderMaintenance', 'Reserved'];
  protected readonly board = signal<WardBoard[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly summary = computed(() => {
    const wards = this.board();
    const free = wards.reduce((n, w) => n + w.available, 0);
    const occupied = wards.reduce((n, w) => n + w.occupied, 0);
    return `${free} beds free · ${occupied} occupied`;
  });

  constructor() {
    this.load();
    const timer = setInterval(() => this.load(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected load(): void {
    this.api.board().subscribe({
      next: (b) => {
        this.board.set(b);
        this.loaded.set(true);
        this.error.set(null);
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'The bed board could not be loaded.')),
    });
  }

  protected setStatus(bed: BoardBed, status: BedStatus): void {
    // Maintenance and reservations take a short note; cleaning status changes do not.
    const note$: Observable<string> =
      status === 'UnderMaintenance' || status === 'Reserved'
        ? this.dialog
            .open(ReasonDialog, {
              data: {
                title: `Bed ${bed.bedNumber}: ${this.labels[status].toLowerCase()}`,
                label: status === 'Reserved' ? 'Reserved for (name or reason)' : 'What needs fixing',
                confirmText: 'Save',
                required: false,
              },
            })
            .afterClosed()
            .pipe(filter((text): text is string => text !== undefined))
        : of('');
    note$.pipe(switchMap((note) => this.api.setBedStatus(bed.id, status, note || null))).subscribe({
      next: () => {
        this.notifications.success(`Bed ${bed.bedNumber}: ${this.labels[status].toLowerCase()}.`);
        this.load();
      },
      error: (err) => this.notifications.error(apiErrorMessage(err)),
    });
  }
}
