import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { filter, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { fromIsoDate, toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { IpdApiService } from '../../ipd/ipd.models';
import { RosterDialog } from '../components/nursing-dialogs';
import { NursingApiService, NursingPermissions, RosterEntry, Shift, SHIFTS, WardOption } from '../nursing.models';

const DAYS = 7;
const addDays = (iso: string, n: number) => {
  const d = fromIsoDate(iso)!;
  d.setDate(d.getDate() + n);
  return toIsoDate(d)!;
};

/** Week view of nurses per ward and shift. Administrators assign and remove; nurses see who is on. Route: /nursing/roster. */
@Component({
  selector: 'app-roster-page',
  imports: [DatePipe, MatButtonModule, MatIconModule, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Nurse Roster" subtitle="Morning 8am–2pm · Evening 2pm–8pm · Night 8pm–8am">
      @if (canManage) {
        <button mat-flat-button type="button" (click)="add(null, today, 'Morning')"><mat-icon>person_add</mat-icon> Assign nurse</button>
      }
    </app-page-header>
    <div class="toolbar">
      <button mat-icon-button type="button" (click)="move(-DAYS)" aria-label="Previous week"><mat-icon>chevron_left</mat-icon></button>
      <span>{{ from() | date: 'd MMM' }} – {{ days()[days().length - 1] | date: 'd MMM y' }}</span>
      <button mat-icon-button type="button" (click)="move(DAYS)" aria-label="Next week"><mat-icon>chevron_right</mat-icon></button>
      <button mat-button type="button" (click)="from.set(today); load()">This week</button>
    </div>
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    <div class="table-scroll">
      <table class="roster">
        <thead>
          <tr>
            <th>Ward</th>
            @for (d of days(); track d) {
              <th [class.today]="d === today">{{ d | date: 'EEE d' }}</th>
            }
          </tr>
        </thead>
        <tbody>
          @for (w of wards(); track w.id) {
            <tr>
              <th scope="row">{{ w.name }}</th>
              @for (d of days(); track d) {
                <td [class.today]="d === today">
                  @for (s of shifts; track s) {
                    <div class="shift">
                      <span class="label">{{ s[0] }}</span>
                      <span class="names">
                        @for (e of cell(w.id, d, s); track e.id) {
                          <span class="chip" [class.charge]="e.isInCharge" [title]="e.isInCharge ? 'In charge' : ''">
                            {{ e.employeeName }}{{ e.isInCharge ? ' ★' : '' }}
                            @if (canManage && d >= today) {
                              <button type="button" class="x" (click)="remove(e)" [attr.aria-label]="'Remove ' + e.employeeName">×</button>
                            }
                          </span>
                        }
                        @if (canManage && d >= today) {
                          <button type="button" class="add" (click)="add(w.id, d, s)" [attr.aria-label]="'Assign ' + s + ' ' + d + ' ' + w.name">+</button>
                        }
                      </span>
                    </div>
                  }
                </td>
              }
            </tr>
          } @empty {
            <tr><td [attr.colspan]="days().length + 1" class="secondary">No wards are set up yet.</td></tr>
          }
        </tbody>
      </table>
    </div>
    <p class="secondary">★ nurse in charge. The roster sets each nurse's default ward; it does not restrict access.</p>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .toolbar { display: flex; align-items: center; gap: 4px; }
    .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .table-scroll { overflow-x: auto; }
    table.roster { border-collapse: collapse; min-width: 900px; width: 100%; }
    th, td { border: 1px solid var(--mat-sys-outline-variant); padding: 6px; vertical-align: top; text-align: left; }
    thead th { font: var(--mat-sys-label-large); background: var(--mat-sys-surface-container); }
    th[scope='row'] { font: var(--mat-sys-label-large); min-width: 120px; }
    .today { background: var(--mat-sys-surface-container-low); }
    .shift { display: flex; gap: 6px; padding: 2px 0; }
    .label { width: 14px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-small); padding-top: 3px; }
    .names { display: flex; flex-wrap: wrap; gap: 4px; }
    .chip { display: inline-flex; align-items: center; gap: 2px; padding: 1px 8px; border-radius: 999px; font: var(--mat-sys-body-small); background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    .chip.charge { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); font-weight: 600; }
    .x, .add { border: 0; background: transparent; cursor: pointer; color: inherit; font-size: 14px; line-height: 1; padding: 0 2px; }
    .add { color: var(--mat-sys-primary); font-weight: 700; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class RosterPage implements OnInit {
  private readonly api = inject(NursingApiService);
  private readonly ipd = inject(IpdApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly DAYS = DAYS;
  protected readonly canManage = inject(AuthService).hasPermission(NursingPermissions.rosterManage);
  protected readonly shifts = SHIFTS;
  protected readonly today = toIsoDate(new Date())!;
  protected readonly from = signal(this.today);
  protected readonly days = computed(() => Array.from({ length: DAYS }, (_, i) => addDays(this.from(), i)));
  protected readonly wards = signal<WardOption[]>([]);
  private readonly entries = signal<RosterEntry[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    // Wards come from IPD: administrators manage the roster without nursing (clinical) access.
    this.ipd.wards().subscribe({
      next: (list) => this.wards.set(list.filter((w) => w.isActive).map((w) => ({ id: w.id, code: w.code, name: w.name }))),
      error: (err) => this.error.set(apiErrorMessage(err)),
    });
    this.load();
  }

  protected cell(wardId: string, date: string, shift: Shift): RosterEntry[] {
    return this.entries().filter((e) => e.wardId === wardId && e.date === date && e.shift === shift);
  }

  protected move(days: number): void {
    this.from.set(addDays(this.from(), days));
    this.load();
  }

  protected load(): void {
    this.api.roster(this.from(), addDays(this.from(), DAYS - 1)).subscribe({
      next: (list) => {
        this.error.set(null);
        this.entries.set(list);
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'The roster could not be loaded.')),
    });
  }

  protected add(wardId: string | null, date: string, shift: Shift): void {
    this.dialog
      .open(RosterDialog, { data: { wards: this.wards(), wardId, date, shift }, width: '520px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }

  protected remove(e: RosterEntry): void {
    this.dialog
      .open(ConfirmDialog, { data: { title: 'Remove from the roster?', message: `${e.employeeName} · ${e.wardName} · ${e.shift} ${e.date}`, confirmText: 'Remove' } })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.removeRoster(e.id)),
      )
      .subscribe({
        next: () => this.load(),
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }
}
