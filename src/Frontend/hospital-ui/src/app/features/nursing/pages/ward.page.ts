import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';

import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ageOf, CurrentShift, NursingApiService, SHIFT_LABELS, WardRound } from '../nursing.models';

const REFRESH_MS = 60_000;
const WARD_KEY = 'hmis.nursing.ward';

/**
 * The nurse's ward: patients with doses due now or overdue, the next dose, and who is on duty. Opens on the ward the
 * signed-in nurse is rostered to (soft roster: any ward can be chosen). Refreshes every minute. Route: /nursing/ward.
 */
@Component({
  selector: 'app-ward-page',
  imports: [DatePipe, FormsModule, RouterLink, MatFormFieldModule, MatSelectModule, MatButtonModule, MatIconModule, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Ward Nursing" [subtitle]="shift() ? shiftLabels[shift()!.shift] + ' shift · ' + (shift()!.date | date: 'EEE d MMM') : ''">
      <button mat-stroked-button type="button" (click)="refresh()"><mat-icon>refresh</mat-icon> Refresh</button>
    </app-page-header>
    @if (shift(); as s) {
      <section class="filters">
        <mat-form-field>
          <mat-label>Ward</mat-label>
          <mat-select [ngModel]="wardId()" (ngModelChange)="select($event)">
            @if (s.myWards.length) {
              <mat-optgroup label="My wards this shift">
                @for (w of s.myWards; track w.id) {
                  <mat-option [value]="w.id">{{ w.name }}</mat-option>
                }
              </mat-optgroup>
            }
            <mat-optgroup label="All wards">
              @for (w of s.allWards; track w.id) {
                <mat-option [value]="w.id">{{ w.name }}</mat-option>
              }
            </mat-optgroup>
          </mat-select>
        </mat-form-field>
        @if (ward(); as w) {
          <div class="duty">
            <mat-icon>badge</mat-icon>
            @for (d of w.onDuty; track d.employeeId) {
              <span>{{ d.name }}{{ d.isInCharge ? ' (in charge)' : '' }}{{ $last ? '' : ',' }}</span>
            } @empty {
              <span class="secondary">No nurse rostered for this shift.</span>
            }
          </div>
        }
      </section>
    }
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    @if (ward(); as w) {
      <div class="cards">
        @for (p of w.patients; track p.admissionId) {
          <a class="card" [routerLink]="['/nursing/patients', p.admissionId]" [class.alert]="p.overdue > 0" [class.due]="p.dueNow > 0 && p.overdue === 0">
            <div class="top">
              <span class="bed">{{ p.bedNumber }}</span>
              <span class="grow">
                <strong>{{ p.patientName }}</strong>
                <span class="secondary">{{ p.gender }}{{ p.dateOfBirth ? ', ' + age(p.dateOfBirth) : '' }} · {{ p.mrNumber }}</span>
              </span>
            </div>
            <div class="secondary">{{ p.attendingDoctorName }} · day {{ dayOf(p.admittedAt) }}{{ p.status !== 'Admitted' ? ' · discharge in progress' : '' }}</div>
            <div class="doses">
              @if (p.overdue) {
                <span class="pill overdue"><mat-icon inline>error</mat-icon> {{ p.overdue }} overdue</span>
              }
              @if (p.dueNow) {
                <span class="pill duenow"><mat-icon inline>alarm</mat-icon> {{ p.dueNow }} due now</span>
              }
              @if (p.nextDoseAt) {
                <span class="pill">Next {{ p.nextDoseAt | date: 'h:mm a' }}</span>
              }
              <span class="secondary">{{ p.activeOrders }} active order{{ p.activeOrders === 1 ? '' : 's' }}</span>
            </div>
          </a>
        } @empty {
          <p class="secondary">No patients in this ward.</p>
        }
      </div>
    } @else if (!error()) {
      <p class="secondary">{{ shift() && !shift()!.allWards.length ? 'No wards are set up yet.' : 'Loading…' }}</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .filters { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 24px; }
    .filters mat-form-field { min-width: 260px; }
    .duty { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; color: var(--mat-sys-on-surface-variant); }
    .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
    .card {
      display: flex; flex-direction: column; gap: 6px; padding: 12px 14px; border-radius: 12px; text-decoration: none; color: inherit;
      border: 1px solid var(--mat-sys-outline-variant); background: var(--mat-sys-surface-container-low);
    }
    .card:hover { background: var(--mat-sys-surface-container); }
    .card.alert { border: 2px solid var(--mat-sys-error); }
    .card.due { border: 2px solid var(--mat-sys-tertiary); }
    .top { display: flex; align-items: center; gap: 12px; }
    .bed {
      min-width: 48px; padding: 6px 8px; border-radius: 8px; text-align: center; font: var(--mat-sys-title-medium);
      background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container);
    }
    .grow { display: flex; flex-direction: column; min-width: 0; }
    .doses { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
    .pill { display: inline-flex; align-items: center; gap: 4px; padding: 2px 10px; border-radius: 999px; font: var(--mat-sys-label-medium); background: var(--mat-sys-surface-container-highest); }
    .pill.overdue { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .pill.duenow { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class WardPage implements OnInit {
  private readonly api = inject(NursingApiService);

  protected readonly shiftLabels = SHIFT_LABELS;
  protected readonly shift = signal<CurrentShift | null>(null);
  protected readonly wardId = signal<string | null>(null);
  protected readonly ward = signal<WardRound | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly age = ageOf;

  constructor() {
    const timer = setInterval(() => this.refresh(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  ngOnInit(): void {
    this.api.currentShift().subscribe({
      next: (s) => {
        this.shift.set(s);
        const remembered = readWard();
        const id = s.myWards[0]?.id ?? (s.allWards.some((w) => w.id === remembered) ? remembered : s.allWards[0]?.id) ?? null;
        if (id) {
          this.select(id);
        }
      },
      error: (err) => this.error.set(apiErrorMessage(err)),
    });
  }

  protected select(id: string): void {
    this.wardId.set(id);
    this.ward.set(null);
    try {
      localStorage.setItem(WARD_KEY, id);
    } catch {
      // Remembering the ward is a convenience only.
    }
    this.refresh();
  }

  protected refresh(): void {
    const id = this.wardId();
    if (!id) {
      return;
    }
    this.api.ward(id).subscribe({
      next: (w) => {
        this.error.set(null);
        this.ward.set(w);
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'The ward could not be loaded.')),
    });
  }

  /** Hospital day number of the stay (admission day = day 1). */
  protected dayOf(admittedAt: string): number {
    const start = new Date(admittedAt);
    start.setHours(0, 0, 0, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return Math.round((today.getTime() - start.getTime()) / 86_400_000) + 1;
  }
}

function readWard(): string | null {
  try {
    return localStorage.getItem(WARD_KEY);
  } catch {
    return null;
  }
}
