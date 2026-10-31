import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse, PaginationQuery } from '../../core/models/api-response.model';

export const AppointmentPermissions = {
  view: 'Appointment.View',
  book: 'Appointment.Book',
  checkIn: 'Appointment.CheckIn',
  cancel: 'Appointment.Cancel',
  consult: 'Appointment.Consult',
  schedulesManage: 'Appointment.Schedules.Manage',
  queueDisplay: 'Appointment.QueueDisplay',
} as const;

export type DayOfWeek = 'Saturday' | 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday';
/** Bangladesh week order. */
export const WEEK_DAYS: DayOfWeek[] = ['Saturday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

export type VisitType = 'New' | 'FollowUp';
export type AppointmentSource = 'Reception' | 'Phone' | 'WalkIn';
export type AppointmentStatus = 'Scheduled' | 'Confirmed' | 'Waiting' | 'InConsultation' | 'Completed' | 'Cancelled' | 'NoShow';

export const STATUS_LABELS: Record<AppointmentStatus, string> = {
  Scheduled: 'Scheduled',
  Confirmed: 'Confirmed',
  Waiting: 'Checked in · waiting',
  InConsultation: 'In consultation',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  NoShow: 'No-show',
};

export interface Schedule {
  id: string;
  doctorId: string;
  doctorName: string | null;
  specialization: string | null;
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  maxPatients: number;
  averageMinutesPerPatient: number;
  room: string | null;
  effectiveFrom: string;
  effectiveTo: string | null;
  isActive: boolean;
  rowVersion: number;
}

export interface ScheduleRequest {
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  maxPatients: number;
  averageMinutesPerPatient: number;
  room: string | null;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface Leave {
  id: string;
  doctorId: string;
  doctorName: string | null;
  date: string;
  scheduleId: string | null;
  sessionLabel: string | null;
  reason: string;
}

export interface SessionAvailability {
  scheduleId: string;
  startTime: string;
  endTime: string;
  room: string | null;
  maxPatients: number;
  booked: number;
  remaining: number;
  nextSerial: number;
  nextEstimatedTime: string;
  isFull: boolean;
  hasEnded: boolean;
  onLeave: boolean;
  leaveReason: string | null;
  isBookable: boolean;
}

export interface Appointment {
  id: string;
  appointmentNumber: string;
  patientId: string;
  patientMRNumber: string | null;
  patientName: string | null;
  patientGender: string | null;
  patientDateOfBirth: string | null;
  doctorId: string;
  doctorName: string | null;
  scheduleId: string;
  room: string | null;
  appointmentDate: string;
  serialNumber: number;
  estimatedTime: string;
  visitType: VisitType;
  source: AppointmentSource;
  fee: number;
  notes: string | null;
  status: AppointmentStatus;
  checkedInAt: string | null;
  consultationStartedAt: string | null;
  completedAt: string | null;
  cancellationReason: string | null;
  rowVersion: number;
}

export interface AppointmentQuery extends PaginationQuery {
  date?: string;
  dateFrom?: string;
  dateTo?: string;
  doctorId?: string;
  patientId?: string;
  status?: AppointmentStatus;
}

export interface MyQueue {
  doctorId: string;
  doctorName: string | null;
  date: string;
  current: Appointment | null;
  waiting: Appointment[];
  others: Appointment[];
}

export interface QueueSession {
  scheduleId: string;
  doctorName: string | null;
  specialization: string | null;
  room: string | null;
  startTime: string;
  endTime: string;
  nowServing: number | null;
  waiting: number[];
  completedCount: number;
}

export interface QueueBoard {
  date: string;
  generatedAt: string;
  sessions: QueueSession[];
}

/** "17:00:00" -> "5:00 PM" */
export function formatTime(value: string): string {
  const [h, m] = value.split(':').map(Number);
  const suffix = h >= 12 ? 'PM' : 'AM';
  return `${((h + 11) % 12) + 1}:${String(m).padStart(2, '0')} ${suffix}`;
}

@Injectable({ providedIn: 'root' })
export class AppointmentApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  // Sessions & leave
  schedules(doctorId?: string): Observable<Schedule[]> {
    return this.get<Schedule[]>('/doctor-schedules', { doctorId });
  }

  createSchedule(request: ScheduleRequest & { doctorId: string }): Observable<Schedule> {
    return this.send<Schedule>('post', '/doctor-schedules', request);
  }

  updateSchedule(id: string, request: ScheduleRequest & { rowVersion: number }): Observable<Schedule> {
    return this.send<Schedule>('put', `/doctor-schedules/${id}`, request);
  }

  setScheduleStatus(id: string, isActive: boolean): Observable<Schedule> {
    return this.send<Schedule>('patch', `/doctor-schedules/${id}/status`, { isActive });
  }

  leaves(doctorId?: string, from?: string): Observable<Leave[]> {
    return this.get<Leave[]>('/doctor-leaves', { doctorId, from });
  }

  addLeave(request: { doctorId: string; date: string; scheduleId: string | null; reason: string }): Observable<Leave> {
    return this.send<Leave>('post', '/doctor-leaves', request);
  }

  removeLeave(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/doctor-leaves/${id}`);
  }

  // Booking
  availability(doctorId: string, date: string): Observable<SessionAvailability[]> {
    return this.get<SessionAvailability[]>('/appointments/availability', { doctorId, date });
  }

  search(query: AppointmentQuery): Observable<PagedResponse<Appointment>> {
    return this.get<PagedResponse<Appointment>>('/appointments', query);
  }

  book(request: {
    patientId: string;
    doctorId: string;
    scheduleId: string;
    appointmentDate: string;
    visitType: VisitType;
    source: AppointmentSource;
    notes: string | null;
  }): Observable<Appointment> {
    return this.send<Appointment>('post', '/appointments', request);
  }

  reschedule(id: string, scheduleId: string, appointmentDate: string, rowVersion: number): Observable<Appointment> {
    return this.send<Appointment>('put', `/appointments/${id}/reschedule`, { scheduleId, appointmentDate, rowVersion });
  }

  action(id: string, action: 'confirm' | 'check-in' | 'no-show' | 'start' | 'complete'): Observable<Appointment> {
    return this.send<Appointment>('post', `/appointments/${id}/${action}`, {});
  }

  cancel(id: string, reason: string): Observable<Appointment> {
    return this.send<Appointment>('post', `/appointments/${id}/cancel`, { reason });
  }

  // Queues
  myQueue(): Observable<MyQueue> {
    return this.get<MyQueue>('/appointments/my-queue', {});
  }

  callNext(): Observable<Appointment> {
    return this.send<Appointment>('post', '/appointments/my-queue/call-next', {});
  }

  queueBoard(): Observable<QueueBoard> {
    return this.get<QueueBoard>('/queue-board', {});
  }

  private get<T>(path: string, query: object): Observable<T> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<ApiResponse<T>>(`${this.base}${path}`, { params }).pipe(map((r) => r.data as T));
  }

  private send<T>(method: 'post' | 'put' | 'patch', path: string, body: unknown): Observable<T> {
    return this.http.request<ApiResponse<T>>(method, `${this.base}${path}`, { body }).pipe(map((r) => r.data as T));
  }
}
