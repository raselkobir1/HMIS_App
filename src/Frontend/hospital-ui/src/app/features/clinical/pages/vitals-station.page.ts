import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { filter, forkJoin } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { todayIso } from '../../../shared/utilities/dates';
import { Appointment, AppointmentApiService, formatTime, STATUS_LABELS } from '../../appointment/appointment.models';
import { ageLabel } from '../../patient/patient.models';
import { ClinicalPermissions } from '../clinical.models';
import { VitalsDialog, VitalsDialogData } from '../components/vitals-dialog';

const REFRESH_MS = 30_000;

/** Nurse station: today's checked-in OPD patients, to record vitals before they see the doctor. Route: /opd/vitals. */
@Component({
  selector: 'app-vitals-station-page',
  imports: [FormsModule, RouterLink, MatTableModule, MatFormFieldModule, MatSelectModule, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="OPD vitals" subtitle="Checked-in patients waiting for or with the doctor today">
      <button mat-stroked-button type="button" (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
    </app-page-header>
    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Doctor</mat-label>
        <mat-select [ngModel]="doctorId()" (ngModelChange)="doctorId.set($event)">
          <mat-option value="">All doctors</mat-option>
          @for (d of doctors(); track d.id) {
            <mat-option [value]="d.id">{{ d.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </section>
    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="table-scroll">
        <table mat-table [dataSource]="rows()">
          <ng-container matColumnDef="serial">
            <th mat-header-cell *matHeaderCellDef>Serial</th>
            <td mat-cell *matCellDef="let a">
              <div class="serial">{{ a.serialNumber }}</div>
              <div class="secondary">{{ formatTime(a.estimatedTime) }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="patient">
            <th mat-header-cell *matHeaderCellDef>Patient</th>
            <td mat-cell *matCellDef="let a">
              <div>{{ a.patientName }}</div>
              <div class="secondary">{{ a.patientMRNumber }} · {{ info(a) }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="doctor">
            <th mat-header-cell *matHeaderCellDef>Doctor</th>
            <td mat-cell *matCellDef="let a">
              <div>{{ a.doctorName }}</div>
              <div class="secondary">{{ a.room ?? '' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let a"><span class="badge" [class.info]="a.status === 'InConsultation'">{{ labels[a.status] }}</span></td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="actions-col wide"></th>
            <td mat-cell *matCellDef="let a" class="actions-col wide">
              @if (canRecord) {
                <button mat-stroked-button type="button" (click)="record(a)"><mat-icon>monitor_heart</mat-icon> Vitals</button>
              }
              @if (canView) {
                <a mat-icon-button [routerLink]="['/opd/encounters', a.id]" [attr.aria-label]="'Open visit of ' + a.patientName"><mat-icon>open_in_new</mat-icon></a>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No checked-in patients right now.' }}</td>
          </tr>
        </table>
      </div>
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    .serial { font: var(--mat-sys-title-medium); }
    .wide { width: 170px; white-space: nowrap; }
  `,
})
export class VitalsStationPage {
  private readonly api = inject(AppointmentApiService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  protected readonly formatTime = formatTime;
  protected readonly labels: Record<string, string> = STATUS_LABELS;
  protected readonly columns = ['serial', 'patient', 'doctor', 'status', 'actions'];
  protected readonly canRecord = this.auth.hasPermission(ClinicalPermissions.vitalsRecord);
  protected readonly canView = this.auth.hasPermission(ClinicalPermissions.view);
  protected readonly all = signal<Appointment[]>([]);
  protected readonly doctorId = signal('');
  protected readonly loading = signal(false);

  protected readonly doctors = computed(() => {
    const map = new Map<string, string>();
    this.all().forEach((a) => map.set(a.doctorId, a.doctorName ?? ''));
    return [...map].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });
  protected readonly rows = computed(() => {
    const doctorId = this.doctorId();
    return this.all().filter((a) => !doctorId || a.doctorId === doctorId);
  });

  constructor() {
    this.load();
    const timer = setInterval(() => this.load(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected load(): void {
    const date = todayIso();
    this.loading.set(true);
    forkJoin([
      this.api.search({ pageNumber: 1, pageSize: 100, date, status: 'Waiting' }),
      this.api.search({ pageNumber: 1, pageSize: 100, date, status: 'InConsultation' }),
    ]).subscribe({
      next: ([waiting, inConsultation]) => {
        this.all.set(
          [...inConsultation.items, ...waiting.items].sort(
            (a, b) => (a.doctorName ?? '').localeCompare(b.doctorName ?? '') || a.serialNumber - b.serialNumber,
          ),
        );
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected info(a: Appointment): string {
    return [a.patientDateOfBirth ? ageLabel(a.patientDateOfBirth) : '', a.patientGender].filter(Boolean).join(' / ');
  }

  protected record(a: Appointment): void {
    this.dialog
      .open<VitalsDialog, VitalsDialogData>(VitalsDialog, {
        data: { encounterId: a.id, patientLabel: `Serial ${a.serialNumber} · ${a.patientName} · ${a.patientMRNumber} · ${a.doctorName}` },
        width: '560px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe();
  }
}
