import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { filter, switchMap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ReasonDialog } from '../../billing/components/reason-dialog';
import { VitalsDialog } from '../../clinical/components/vitals-dialog';
import { InpatientChart, MedicationOrder, NOTE_KIND_LABELS, NursingApiService, NursingPermissions } from '../nursing.models';
import { MedicationOrderDialog, ProgressNoteDialog } from './doctor-dialogs';

const reasonDialog = (title: string, confirmText: string, message?: string) => ({
  data: { title, message, label: 'Reason', confirmText, required: true, destructive: true },
});

/** Medication orders and progress notes (Clinical). Doctors with Clinical.Inpatient.Write order, stop and sign notes. */
@Component({
  selector: 'app-chart-doctor-tab',
  imports: [DatePipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let c = chart();
    <section>
      <div class="section-head">
        <h2>Medication orders</h2>
        @if (canWrite()) {
          <button mat-flat-button type="button" (click)="order()"><mat-icon>medication</mat-icon> Order medication</button>
        }
      </div>
      @for (o of orders(); track o.id) {
        <div class="row" [class.inactive]="o.status !== 'Active'">
          <div class="grow">
            <strong>{{ o.medicineName }}{{ o.strength ? ' ' + o.strength : '' }}</strong>
            <span class="badge" [class]="'badge ' + o.status">{{ o.status }}</span>
            <div>{{ o.dose }} · {{ o.route }} · {{ o.frequency }}{{ o.durationDays ? ' × ' + o.durationDays + ' days' : '' }}</div>
            @if (o.prnIndication) {
              <div class="secondary">Give when: {{ o.prnIndication }}</div>
            }
            @if (o.instructions) {
              <div class="secondary">{{ o.instructions }}</div>
            }
            <div class="secondary">
              {{ o.orderedByName }} · from {{ o.startsAt | date: 'd MMM, h:mm a' }}{{ o.endsAt ? ' to ' + (o.endsAt | date: 'd MMM, h:mm a') : '' }}
              @if (o.stoppedAt) {
                · stopped by {{ o.stoppedByName }}: {{ o.stopReason }}
              }
            </div>
          </div>
          @if (canWrite() && o.status === 'Active') {
            <button mat-button type="button" (click)="stop(o)" [attr.aria-label]="'Stop ' + o.medicineName">Stop</button>
          }
        </div>
      } @empty {
        <p class="secondary">No medication orders.</p>
      }
    </section>

    <section>
      <div class="section-head">
        <h2>Progress notes</h2>
        @if (canWrite()) {
          <button mat-stroked-button type="button" (click)="writeNote()"><mat-icon>edit_note</mat-icon> Write note</button>
        }
      </div>
      @for (n of c.progressNotes; track n.id) {
        <article class="note" [class.error]="n.isEnteredInError">
          <header>
            <strong>{{ kinds[n.kind] }}</strong> · {{ n.doctorName }} · {{ n.writtenAt | date: 'd MMM y, h:mm a' }}
            @if (n.isEnteredInError) {
              <span class="badge Stopped">Entered in error: {{ n.errorReason }}</span>
            } @else if (canWrite() && n.doctorId === c.currentDoctorId) {
              <button mat-button type="button" class="small" (click)="noteInError(n.id)">Entered in error</button>
            }
          </header>
          @if (n.subjective) { <p><b>S:</b> {{ n.subjective }}</p> }
          @if (n.objective) { <p><b>O:</b> {{ n.objective }}</p> }
          @if (n.assessment) { <p><b>A:</b> {{ n.assessment }}</p> }
          @if (n.plan) { <p><b>P:</b> {{ n.plan }}</p> }
        </article>
      } @empty {
        <p class="secondary">No progress notes yet.</p>
      }
    </section>
  `,
  styleUrl: './chart-tab.scss',
})
export class ChartDoctorTab {
  readonly chart = input.required<InpatientChart>();
  readonly patientLabel = input.required<string>();
  readonly changed = output<void>();

  private readonly api = inject(NursingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly kinds = NOTE_KIND_LABELS;
  protected readonly canWrite = computed(
    () => this.chart().stay.isActive && this.chart().isDoctor && this.auth.hasPermission(NursingPermissions.inpatientWrite),
  );
  protected readonly orders = computed(() => {
    const rank = { Active: 0, Completed: 1, Stopped: 2 };
    return [...this.chart().medicationOrders].sort((a, b) => rank[a.status] - rank[b.status]);
  });

  protected order(): void {
    const c = this.chart();
    this.dialog
      .open(MedicationOrderDialog, { data: { admissionId: c.stay.admissionId, patientLabel: this.patientLabel(), allergies: c.allergies }, width: '640px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.changed.emit());
  }

  protected stop(o: MedicationOrder): void {
    this.dialog
      .open(ReasonDialog, reasonDialog(`Stop ${o.medicineName}?`, 'Stop order', 'Doses after now are no longer scheduled. To change a dose, stop and order again.'))
      .afterClosed()
      .pipe(
        filter((reason): reason is string => !!reason),
        switchMap((reason) => this.api.stopOrder(o.id, reason, o.rowVersion)),
      )
      .subscribe({
        next: () => {
          this.notifications.success(`${o.medicineName} stopped.`);
          this.changed.emit();
        },
        error: (err) => {
          this.notifications.error(apiErrorMessage(err));
          this.changed.emit();
        },
      });
  }

  protected writeNote(): void {
    this.dialog
      .open(ProgressNoteDialog, { data: { admissionId: this.chart().stay.admissionId, patientLabel: this.patientLabel() }, width: '640px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.changed.emit());
  }

  protected noteInError(id: string): void {
    this.dialog
      .open(ReasonDialog, reasonDialog('Mark note as entered in error?', 'Mark in error', 'The note stays on the record, struck through.'))
      .afterClosed()
      .pipe(
        filter((reason): reason is string => !!reason),
        switchMap((reason) => this.api.progressNoteInError(id, reason)),
      )
      .subscribe({
        next: () => this.changed.emit(),
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }
}

/** Ward vitals for the stay (Clinical vital signs, encounter = admission). */
@Component({
  selector: 'app-chart-vitals-tab',
  imports: [DatePipe, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <div class="section-head">
        <h2>Vital signs</h2>
        @if (canRecord()) {
          <button mat-flat-button type="button" (click)="record()"><mat-icon>monitor_heart</mat-icon> Record vitals</button>
        }
      </div>
      <div class="table-scroll">
        <table class="grid">
          <thead>
            <tr><th>Time</th><th>BP</th><th>Pulse</th><th>Temp °F</th><th>RR</th><th>SpO₂</th><th>Weight</th><th>By</th><th></th></tr>
          </thead>
          <tbody>
            @for (v of chart().vitals; track v.id) {
              <tr [class.error]="v.isEnteredInError">
                <td>{{ v.recordedAt | date: 'd MMM, h:mm a' }}</td>
                <td>{{ v.systolic ? v.systolic + '/' + v.diastolic : '' }}</td>
                <td>{{ v.pulse }}</td>
                <td [class.alert]="(v.temperatureF ?? 0) >= 100.4">{{ v.temperatureF }}</td>
                <td>{{ v.respiratoryRate }}</td>
                <td [class.alert]="v.spO2 !== null && v.spO2 < 94">{{ v.spO2 }}</td>
                <td>{{ v.weightKg }}</td>
                <td>{{ v.recordedBy }}{{ v.notes ? ' · ' + v.notes : '' }}{{ v.isEnteredInError ? ' · in error: ' + v.errorReason : '' }}</td>
                <td>
                  @if (canRecord() && !v.isEnteredInError) {
                    <button mat-icon-button type="button" (click)="markError(v.id)" aria-label="Mark vitals as entered in error"><mat-icon>block</mat-icon></button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="9" class="secondary">No vitals recorded during this stay.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
  styleUrl: './chart-tab.scss',
})
export class ChartVitalsTab {
  readonly chart = input.required<InpatientChart>();
  readonly patientLabel = input.required<string>();
  readonly changed = output<void>();

  private readonly api = inject(NursingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly canRecord = computed(() => this.chart().stay.isActive && this.auth.hasPermission(NursingPermissions.vitalsRecord));

  protected record(): void {
    this.dialog
      .open(VitalsDialog, { data: { encounterId: this.chart().stay.admissionId, patientLabel: this.patientLabel(), inpatient: true }, width: '560px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.changed.emit());
  }

  protected markError(id: string): void {
    this.dialog
      .open(ReasonDialog, reasonDialog('Mark vitals as entered in error?', 'Mark in error'))
      .afterClosed()
      .pipe(
        filter((reason): reason is string => !!reason),
        switchMap((reason) => this.api.vitalsInError(id, reason)),
      )
      .subscribe({
        next: () => this.changed.emit(),
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }
}
