import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, OnInit, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';

import { NotificationService } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { toIsoDate } from '../../../shared/utilities/dates';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../../administration/administration-api.service';
import { DepartmentLookupItem } from '../../administration/administration.models';
import { Doctor, DoctorApiService } from '../../hr/doctors.models';
import { PatientPicker } from '../../patient/components/patient-picker';
import { PatientApiService } from '../../patient/patient-api.service';
import { PatientListItem } from '../../patient/patient.models';
import { AppointmentApiService, AppointmentSource, formatTime, SessionAvailability, VisitType } from '../appointment.models';

/** Book an appointment or register a walk-in. Route: /appointments/book (optional ?patientId=). */
@Component({
  selector: 'app-book-appointment-page',
  imports: [
    CurrencyPipe,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonToggleModule,
    MatButtonModule,
    MatIconModule,
    PageHeader,
    PatientPicker,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './book-appointment.page.html',
  styleUrl: './book-appointment.page.scss',
})
export class BookAppointmentPage implements OnInit {
  /** Query parameter when booking from a patient profile. */
  readonly patientId = input<string>();

  private readonly api = inject(AppointmentApiService);
  private readonly doctorApi = inject(DoctorApiService);
  private readonly patientApi = inject(PatientApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly picker = viewChild(PatientPicker);

  protected readonly formatTime = formatTime;
  protected readonly today = new Date();
  protected readonly maxDate = new Date(Date.now() + 90 * 86_400_000);

  protected readonly patient = signal<PatientListItem | null>(null);
  protected readonly departments = signal<DepartmentLookupItem[]>([]);
  protected readonly departmentId = signal<string>('');
  protected readonly doctors = signal<Doctor[]>([]);
  protected readonly doctorId = signal<string>('');
  protected readonly date = signal<Date>(new Date());
  protected readonly sessions = signal<SessionAvailability[]>([]);
  protected readonly sessionId = signal<string | null>(null);
  protected readonly visitType = signal<VisitType>('New');
  protected readonly walkIn = signal(false);
  protected readonly notes = signal('');
  protected readonly loadingSessions = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly doctor = computed(() => this.doctors().find((d) => d.employeeId === this.doctorId()) ?? null);
  protected readonly session = computed(() => this.sessions().find((s) => s.scheduleId === this.sessionId()) ?? null);
  protected readonly fee = computed(() => {
    const d = this.doctor();
    return d ? (this.visitType() === 'New' ? d.newPatientFee : d.followUpFee) : null;
  });
  protected readonly canBook = computed(() => !!this.patient() && !!this.doctor() && !!this.session()?.isBookable && !this.saving());

  constructor() {
    inject(AdministrationApiService).departmentLookup().subscribe({ next: (d) => this.departments.set(d), error: () => undefined });

    effect(() => {
      const departmentId = this.departmentId();
      this.doctorApi.bookable(departmentId || undefined).subscribe({ next: (d) => this.doctors.set(d), error: () => this.doctors.set([]) });
    });

    // Walk-ins are for today only.
    effect(() => {
      if (this.walkIn()) {
        this.date.set(new Date());
      }
    });

    effect(() => {
      const doctorId = this.doctorId();
      const date = this.date();
      this.sessionId.set(null);
      this.sessions.set([]);
      if (!doctorId || !date) {
        return;
      }
      this.loadingSessions.set(true);
      this.api.availability(doctorId, toIsoDate(date)!).subscribe({
        next: (sessions) => {
          this.sessions.set(sessions);
          this.sessionId.set(sessions.find((s) => s.isBookable)?.scheduleId ?? null);
          this.loadingSessions.set(false);
        },
        error: () => this.loadingSessions.set(false),
      });
    });
  }

  ngOnInit(): void {
    const id = this.patientId();
    if (id) {
      this.patientApi.getById(id).subscribe((p) =>
        this.picker()?.setPatient({
          id: p.id,
          mrNumber: p.mrNumber,
          fullName: p.fullName,
          gender: p.gender,
          dateOfBirth: p.dateOfBirth,
          isDateOfBirthEstimated: p.isDateOfBirthEstimated,
          mobileNumber: p.mobileNumber,
          district: p.presentAddress.district,
          status: p.status,
        }),
      );
    }
  }

  protected sessionState(s: SessionAvailability): string {
    if (s.onLeave) return `Doctor on leave${s.leaveReason ? ' — ' + s.leaveReason : ''}`;
    if (s.hasEnded) return 'Session has ended';
    if (s.isFull) return 'Full';
    return `${s.remaining} of ${s.maxPatients} places left · next serial ${s.nextSerial} at about ${formatTime(s.nextEstimatedTime)}`;
  }

  protected book(): void {
    const patient = this.patient();
    const session = this.session();
    if (!patient || !session || !this.canBook()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const source: AppointmentSource = this.walkIn() ? 'WalkIn' : 'Reception';
    this.api
      .book({
        patientId: patient.id,
        doctorId: this.doctorId(),
        scheduleId: session.scheduleId,
        appointmentDate: toIsoDate(this.date())!,
        visitType: this.visitType(),
        source,
        notes: this.notes().trim() || null,
      })
      .subscribe({
        next: (a) => {
          this.notifications.success(
            `${a.appointmentNumber}: serial ${a.serialNumber}, about ${formatTime(a.estimatedTime)}${a.status === 'Waiting' ? ' — checked in' : ''}.`,
          );
          void this.router.navigate(['/appointments'], { queryParams: { date: a.appointmentDate, doctorId: a.doctorId } });
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err, 'The appointment could not be booked.'));
          // Availability may have changed (e.g. the session filled up meanwhile).
          this.date.set(new Date(this.date()));
        },
      });
  }
}
