import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';

import { Appointment, AppointmentApiService, formatTime, STATUS_LABELS } from '../appointment.models';

/** Appointment history on the patient profile (latest first, as returned by the API). */
@Component({
  selector: 'app-patient-appointments-panel',
  imports: [CurrencyPipe, MatTableModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-card">
      <div class="table-scroll">
        <table mat-table [dataSource]="items()">
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let a">
              <div>{{ a.appointmentDate }}</div>
              <div class="secondary">Serial {{ a.serialNumber }} · {{ formatTime(a.estimatedTime) }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="doctor">
            <th mat-header-cell *matHeaderCellDef>Doctor</th>
            <td mat-cell *matCellDef="let a">{{ a.doctorName }}</td>
          </ng-container>
          <ng-container matColumnDef="visit">
            <th mat-header-cell *matHeaderCellDef>Visit</th>
            <td mat-cell *matCellDef="let a">
              {{ a.visitType === 'New' ? 'New' : 'Follow-up' }} · {{ a.fee | currency: 'BDT' : 'symbol-narrow' : '1.0-0' }}
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let a">{{ labels[a.status] }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No appointments yet.' }}</td>
          </tr>
        </table>
      </div>
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class PatientAppointmentsPanel {
  readonly patientId = input.required<string>();

  private readonly api = inject(AppointmentApiService);

  protected readonly formatTime = formatTime;
  protected readonly labels: Record<string, string> = STATUS_LABELS;
  protected readonly columns = ['date', 'doctor', 'visit', 'status'];
  protected readonly items = signal<Appointment[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    effect(() => {
      this.loading.set(true);
      this.api.search({ pageNumber: 1, pageSize: 50, patientId: this.patientId() }).subscribe({
        next: (page) => {
          this.items.set(page.items);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    });
  }
}
