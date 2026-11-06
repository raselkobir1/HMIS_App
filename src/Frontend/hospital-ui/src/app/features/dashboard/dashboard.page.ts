import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { ApiResponse } from '../../core/models/api-response.model';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { todayIso } from '../../shared/utilities/dates';
import { BillingApiService } from '../billing/billing.models';
import { TakaPipe } from '../billing/components/taka.pipe';
import { IpdApiService } from '../ipd/ipd.models';

interface PatientCounts {
  registeredToday: number;
  activePatients: number;
}

interface AppointmentSummary {
  date: string;
  total: number;
  byStatus: { status: string; count: number }[];
}

const STATUS_LABELS: Record<string, string> = {
  Scheduled: 'Scheduled',
  Confirmed: 'Confirmed',
  Waiting: 'Waiting',
  InConsultation: 'With doctor',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  NoShow: 'No-show',
};

/** Today at a glance. Each card appears only for users who may see its data; each opens the related screen. */
@Component({
  selector: 'app-dashboard-page',
  imports: [DecimalPipe, RouterLink, MatCardModule, MatIconModule, PageHeader, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Dashboard" [subtitle]="'Welcome, ' + (user()?.fullName ?? 'User')" />
    <div class="grid">
      @if (patients(); as p) {
        <a class="card" routerLink="/patients">
          <mat-icon>personal_injury</mat-icon>
          <span class="label">Patients registered today</span>
          <strong>{{ p.registeredToday | number }}</strong>
          <span class="secondary">{{ p.activePatients | number }} active records</span>
        </a>
      }
      @if (appointments(); as a) {
        <a class="card" routerLink="/appointments">
          <mat-icon>event</mat-icon>
          <span class="label">Appointments today</span>
          <strong>{{ a.total | number }}</strong>
          <span class="chips">
            @for (s of a.byStatus; track s.status) {
              @if (s.count) {
                <span class="chip">{{ statusLabels[s.status] ?? s.status }} {{ s.count }}</span>
              }
            }
          </span>
        </a>
      }
      @if (beds(); as b) {
        <a class="card" routerLink="/ipd/beds">
          <mat-icon>bed</mat-icon>
          <span class="label">Bed occupancy</span>
          <strong>{{ b.occupied }} / {{ b.total }}</strong>
          <span class="meter" role="img" [attr.aria-label]="b.percent + '% occupied'"><span [style.width.%]="b.percent"></span></span>
          <span class="secondary">{{ b.available }} free · {{ b.other }} cleaning, maintenance or reserved</span>
        </a>
      }
      @if (collections(); as c) {
        <a class="card" routerLink="/billing/collections">
          <mat-icon>account_balance_wallet</mat-icon>
          <span class="label">{{ c.allCashiers ? 'Collections today' : 'My collections today' }}</span>
          <strong>{{ c.net | taka }}</strong>
          <span class="secondary">{{ c.totalReceived | taka }} received · {{ c.totalRefunded | taka }} refunded</span>
        </a>
      }
      @if (approvals(); as a) {
        <a class="card" [class.attention]="a.total > 0" routerLink="/billing/approvals">
          <mat-icon>approval</mat-icon>
          <span class="label">Waiting for approval</span>
          <strong>{{ a.total }}</strong>
          <span class="secondary">{{ a.discounts }} discount{{ a.discounts === 1 ? '' : 's' }} · {{ a.refunds }} refund{{ a.refunds === 1 ? '' : 's' }}</span>
        </a>
      }
      @if (can.nursing) {
        <a class="card" routerLink="/nursing/ward">
          <mat-icon>medication</mat-icon>
          <span class="label">Ward nursing</span>
          <strong class="small">My ward</strong>
          <span class="secondary">Doses due and overdue, patient charts</span>
        </a>
      }
    </div>
    @if (empty()) {
      <mat-card appearance="outlined">
        <mat-card-content>Use the menu to open your work. Summary cards appear here for the areas you have access to.</mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 12px; }
    .card {
      display: flex; flex-direction: column; gap: 4px; padding: 16px; border-radius: 12px; text-decoration: none; color: inherit;
      border: 1px solid var(--mat-sys-outline-variant); background: var(--mat-sys-surface-container-low);
    }
    .card:hover { background: var(--mat-sys-surface-container); }
    .card.attention { border: 2px solid var(--mat-sys-tertiary); }
    .card mat-icon { color: var(--mat-sys-primary); }
    .label { font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }
    strong { font: var(--mat-sys-headline-medium); }
    strong.small { font: var(--mat-sys-title-large); }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .chips { display: flex; flex-wrap: wrap; gap: 4px; }
    .chip { padding: 1px 8px; border-radius: 999px; font: var(--mat-sys-label-small); background: var(--mat-sys-surface-container-highest); }
    .meter { height: 6px; border-radius: 3px; background: var(--mat-sys-surface-container-highest); overflow: hidden; }
    .meter span { display: block; height: 100%; background: var(--mat-sys-primary); }
  `,
})
export class DashboardPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly billing = inject(BillingApiService);
  private readonly ipd = inject(IpdApiService);
  private readonly api = environment.apiBaseUrl;

  protected readonly user = this.auth.user;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly can = {
    patients: this.auth.hasPermission('Patient.View'),
    appointments: this.auth.hasPermission('Appointment.View'),
    beds: this.auth.hasPermission('Ipd.View'),
    collections: this.auth.hasPermission('Billing.Payment.Collect') || this.auth.hasPermission('Billing.Reports.View'),
    approvals: this.auth.hasAnyPermission(['Billing.Discount.Approve', 'Billing.Refund.Approve']),
    nursing: this.auth.hasPermission('Nursing.View'),
  };

  protected readonly patients = signal<PatientCounts | null>(null);
  protected readonly appointments = signal<AppointmentSummary | null>(null);
  protected readonly beds = signal<{ total: number; occupied: number; available: number; other: number; percent: number } | null>(null);
  protected readonly collections = signal<{ net: number; totalReceived: number; totalRefunded: number; allCashiers: boolean } | null>(null);
  protected readonly approvals = signal<{ total: number; discounts: number; refunds: number } | null>(null);
  protected readonly empty = computed(() => !Object.values(this.can).some(Boolean));

  ngOnInit(): void {
    const today = todayIso();
    if (this.can.patients) {
      this.fetch<PatientCounts>('/patients/counts').subscribe((p) => this.patients.set(p));
    }
    if (this.can.appointments) {
      this.fetch<AppointmentSummary>(`/appointments/summary?date=${today}`).subscribe((a) => this.appointments.set(a));
    }
    if (this.can.beds) {
      this.ipd.board().pipe(catchError(() => of(null))).subscribe((wards) => {
        if (!wards) {
          return;
        }
        const occupied = wards.reduce((n, w) => n + w.occupied, 0);
        const available = wards.reduce((n, w) => n + w.available, 0);
        const other = wards.reduce((n, w) => n + w.other, 0);
        const total = occupied + available + other;
        this.beds.set({ total, occupied, available, other, percent: total ? Math.round((occupied / total) * 100) : 0 });
      });
    }
    if (this.can.collections) {
      this.billing.collections(today, today).pipe(catchError(() => of(null))).subscribe((c) => c && this.collections.set(c));
    }
    if (this.can.approvals) {
      this.billing.approvals().pipe(catchError(() => of(null))).subscribe((a) => {
        if (a) {
          const refunds = a.refundsToApprove.length;
          this.approvals.set({ total: a.discounts.length + refunds, discounts: a.discounts.length, refunds });
        }
      });
    }
  }

  private fetch<T>(path: string) {
    return this.http.get<ApiResponse<T>>(`${this.api}${path}`).pipe(
      map((r) => r.data as T),
      catchError(() => of(null as T)),
    );
  }
}
