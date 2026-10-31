import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ClinicalApiService, ConsultationSummary } from '../clinical.models';

/** Consultation history on the patient profile. Loading it is audited by the API. */
@Component({
  selector: 'app-patient-visits-panel',
  imports: [MatTableModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <p class="state error">{{ message }}</p>
    } @else {
      <div class="table-card">
        <div class="table-scroll">
          <table mat-table [dataSource]="items()">
            <ng-container matColumnDef="date">
              <th mat-header-cell *matHeaderCellDef>Date</th>
              <td mat-cell *matCellDef="let c">
                <a [routerLink]="['/opd/consultations', c.id]">{{ c.encounterDate }}</a>
                <div class="secondary">{{ c.encounterNumber }}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="doctor">
              <th mat-header-cell *matHeaderCellDef>Doctor</th>
              <td mat-cell *matCellDef="let c">{{ c.doctorName }}</td>
            </ng-container>
            <ng-container matColumnDef="diagnosis">
              <th mat-header-cell *matHeaderCellDef>Primary diagnosis</th>
              <td mat-cell *matCellDef="let c">{{ c.primaryDiagnosis ?? '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="rx">
              <th mat-header-cell *matHeaderCellDef>Medicines</th>
              <td mat-cell *matCellDef="let c">{{ c.medicineCount }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let c">
                <span class="badge" [class.ok]="c.status === 'Finalized'" [class.muted]="c.status !== 'Finalized'">{{ c.status }}</span>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
            <tr class="mat-row" *matNoDataRow>
              <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No consultations yet.' }}</td>
            </tr>
          </table>
        </div>
      </div>
    }
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class PatientVisitsPanel {
  readonly patientId = input.required<string>();

  private readonly api = inject(ClinicalApiService);

  protected readonly columns = ['date', 'doctor', 'diagnosis', 'rx', 'status'];
  protected readonly items = signal<ConsultationSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      this.loading.set(true);
      this.api.patientHistory(this.patientId()).subscribe({
        next: (list) => {
          this.items.set(list);
          this.loading.set(false);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(err, 'Visits could not be loaded.'));
        },
      });
    });
  }
}
