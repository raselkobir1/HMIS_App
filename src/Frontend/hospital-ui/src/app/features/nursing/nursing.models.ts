import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { VitalSign, VitalSignRequest } from '../clinical/clinical.models';

export const NursingPermissions = {
  view: 'Nursing.View',
  record: 'Nursing.Record',
  rosterManage: 'Nursing.Roster.Manage',
  clinicalView: 'Clinical.View',
  vitalsRecord: 'Clinical.Vitals.Record',
  inpatientWrite: 'Clinical.Inpatient.Write',
} as const;

export type Shift = 'Morning' | 'Evening' | 'Night';
export const SHIFTS: Shift[] = ['Morning', 'Evening', 'Night'];
export const SHIFT_LABELS: Record<Shift, string> = { Morning: 'Morning (8am–2pm)', Evening: 'Evening (2pm–8pm)', Night: 'Night (8pm–8am)' };

export type ProgressNoteKind = 'WardRound' | 'OnCall' | 'ConsultantReview' | 'Procedure';
export const NOTE_KIND_LABELS: Record<ProgressNoteKind, string> = {
  WardRound: 'Ward round',
  OnCall: 'On-call',
  ConsultantReview: 'Consultant review',
  Procedure: 'Procedure',
};
export type NursingNoteKind = 'Assessment' | 'CarePlan' | 'Progress' | 'Incident';
export const NURSING_NOTE_LABELS: Record<NursingNoteKind, string> = {
  Assessment: 'Assessment',
  CarePlan: 'Care plan',
  Progress: 'Progress',
  Incident: 'Incident',
};
export type AdministrationStatus = 'Given' | 'Omitted' | 'Refused' | 'Held';
export type DoseState = AdministrationStatus | 'Due' | 'Overdue' | 'Upcoming' | 'Cancelled';
export type FluidDirection = 'Intake' | 'Output';

/** Standard ward times (server: NursingClock). */
export const FREQUENCIES: { value: string; label: string }[] = [
  { value: 'OD', label: 'OD — once daily (9am)' },
  { value: 'BD', label: 'BD — twice daily (9am, 9pm)' },
  { value: 'TDS', label: 'TDS — three times (8am, 2pm, 10pm)' },
  { value: 'QID', label: 'QID — four times (12am, 6am, 12pm, 6pm)' },
  { value: 'Q4H', label: '4-hourly from start' },
  { value: 'Q6H', label: '6-hourly from start' },
  { value: 'Q8H', label: '8-hourly from start' },
  { value: 'Q12H', label: '12-hourly from start' },
  { value: 'STAT', label: 'STAT — once, now' },
  { value: 'PRN', label: 'PRN — when needed' },
];

export interface Allergy {
  allergen: string;
  type: string;
  severity: string;
  reaction: string | null;
}

// ---------- Doctor's side (Clinical) ----------

export interface InpatientStay {
  admissionId: string;
  admissionNumber: string;
  status: string;
  isActive: boolean;
  admittedAt: string;
  releasedAt: string | null;
  wardName: string | null;
  bedNumber: string | null;
  attendingDoctorId: string;
  attendingDoctorName: string | null;
}

export interface ProgressNote {
  id: string;
  kind: ProgressNoteKind;
  subjective: string | null;
  objective: string | null;
  assessment: string | null;
  plan: string | null;
  doctorId: string;
  doctorName: string | null;
  writtenAt: string;
  isEnteredInError: boolean;
  errorReason: string | null;
}

export interface MedicationOrder {
  id: string;
  medicineName: string;
  strength: string | null;
  form: string | null;
  route: string;
  dose: string;
  frequency: string;
  startsAt: string;
  durationDays: number | null;
  endsAt: string | null;
  status: 'Active' | 'Stopped' | 'Completed';
  prnIndication: string | null;
  instructions: string | null;
  orderedByName: string | null;
  orderedAt: string;
  stoppedAt: string | null;
  stoppedByName: string | null;
  stopReason: string | null;
  rowVersion: number;
}

export interface MedicationOrderRequest {
  medicineName: string;
  strength: string | null;
  form: string | null;
  route: string;
  dose: string;
  frequency: string;
  startsAt: string | null;
  durationDays: number | null;
  prnIndication: string | null;
  instructions: string | null;
}

export interface InpatientChart {
  stay: InpatientStay;
  patient: { id: string; mrNumber: string; fullName: string; gender: string; dateOfBirth: string | null; mobileNumber: string };
  allergies: Allergy[];
  progressNotes: ProgressNote[];
  medicationOrders: MedicationOrder[];
  vitals: VitalSign[];
  isDoctor: boolean;
  currentDoctorId: string | null;
}

// ---------- Nursing ----------

export interface NursingPatient {
  admissionId: string;
  admissionNumber: string;
  status: string;
  isActive: boolean;
  patientId: string;
  patientName: string | null;
  mrNumber: string | null;
  gender: string | null;
  dateOfBirth: string | null;
  wardName: string | null;
  bedNumber: string | null;
  attendingDoctorName: string | null;
  admittedAt: string;
  allergies: Allergy[];
}

export interface WardOption {
  id: string;
  code: string;
  name: string;
}

export interface CurrentShift {
  date: string;
  shift: Shift;
  startsAt: string;
  endsAt: string;
  myWards: WardOption[];
  allWards: WardOption[];
}

export interface WardPatient {
  admissionId: string;
  admissionNumber: string;
  patientId: string;
  patientName: string | null;
  mrNumber: string | null;
  gender: string | null;
  dateOfBirth: string | null;
  bedNumber: string | null;
  attendingDoctorName: string | null;
  status: string;
  admittedAt: string;
  dueNow: number;
  overdue: number;
  nextDoseAt: string | null;
  activeOrders: number;
}

export interface WardRound {
  ward: WardOption;
  date: string;
  shift: Shift;
  onDuty: { employeeId: string; name: string | null; isInCharge: boolean }[];
  patients: WardPatient[];
}

export interface Administration {
  id: string;
  orderId: string;
  scheduledAt: string | null;
  status: AdministrationStatus;
  occurredAt: string;
  doseGiven: string | null;
  reason: string | null;
  notes: string | null;
  recordedBy: string | null;
  recordedAt: string;
  isEnteredInError: boolean;
  errorReason: string | null;
  isMine: boolean;
}

export interface DoseSlot {
  scheduledAt: string;
  state: DoseState;
  entry: Administration | null;
}

export interface MarOrder {
  id: string;
  medicineName: string;
  strength: string | null;
  form: string | null;
  route: string;
  dose: string;
  frequency: string;
  startsAt: string;
  endsAt: string | null;
  status: 'Active' | 'Stopped' | 'Completed';
  prnIndication: string | null;
  instructions: string | null;
  orderedByName: string | null;
  slots: DoseSlot[];
  entries: Administration[];
}

export interface Mar {
  patient: NursingPatient;
  date: string;
  orders: MarOrder[];
}

export interface AdministrationRequest {
  orderId: string;
  scheduledAt: string | null;
  status: AdministrationStatus;
  occurredAt: string | null;
  doseGiven: string | null;
  reason: string | null;
  notes: string | null;
}

export interface FluidEntry {
  id: string;
  measuredAt: string;
  direction: FluidDirection;
  category: string;
  volumeMl: number;
  notes: string | null;
  recordedBy: string | null;
  isEnteredInError: boolean;
  errorReason: string | null;
  isMine: boolean;
}

export interface FluidTotals {
  label: string;
  intake: number;
  output: number;
  balance: number;
}

export interface FluidDay {
  patient: NursingPatient;
  date: string;
  from: string;
  to: string;
  total: FluidTotals;
  byShift: FluidTotals[];
  entries: FluidEntry[];
  intakeCategories: string[];
  outputCategories: string[];
}

export interface NursingNote {
  id: string;
  kind: NursingNoteKind;
  text: string;
  writtenBy: string | null;
  writtenAt: string;
  isEnteredInError: boolean;
  errorReason: string | null;
  isMine: boolean;
}

export interface HandoverItem {
  admissionId: string;
  patientName: string | null;
  mrNumber: string | null;
  bedNumber: string | null;
  note: string;
}

export interface Handover {
  id: string;
  wardId: string;
  wardName: string | null;
  date: string;
  shift: Shift;
  summary: string | null;
  items: HandoverItem[];
  writtenBy: string | null;
  writtenAt: string;
  receivedBy: string | null;
  receivedAt: string | null;
  isMine: boolean;
  rowVersion: number;
}

export interface HandoverRequest {
  wardId: string;
  date: string;
  shift: Shift;
  summary: string | null;
  items: { admissionId: string; note: string }[];
  rowVersion: number;
}

export interface RosterEntry {
  id: string;
  wardId: string;
  wardName: string | null;
  date: string;
  shift: Shift;
  employeeId: string;
  employeeName: string | null;
  employeeCode: string | null;
  isInCharge: boolean;
}

/** Recorded or pending dose states, for chips and legends. */
export const DOSE_STATE_LABELS: Record<DoseState, string> = {
  Given: 'Given',
  Omitted: 'Omitted',
  Refused: 'Refused',
  Held: 'Held',
  Due: 'Due now',
  Overdue: 'Overdue',
  Upcoming: 'Upcoming',
  Cancelled: 'Cancelled',
};

@Injectable({ providedIn: 'root' })
export class NursingApiService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  // Doctor's side (Clinical module)
  chart(admissionId: string): Observable<InpatientChart> {
    return this.get(`${this.api}/ipd/admissions/${admissionId}/chart`);
  }

  addProgressNote(admissionId: string, request: Omit<ProgressNote, 'id' | 'doctorId' | 'doctorName' | 'writtenAt' | 'isEnteredInError' | 'errorReason'>): Observable<ProgressNote> {
    return this.post(`${this.api}/ipd/admissions/${admissionId}/progress-notes`, request);
  }

  progressNoteInError(id: string, reason: string): Observable<ProgressNote> {
    return this.post(`${this.api}/progress-notes/${id}/entered-in-error`, { reason });
  }

  orderMedication(admissionId: string, request: MedicationOrderRequest): Observable<MedicationOrder> {
    return this.post(`${this.api}/ipd/admissions/${admissionId}/medication-orders`, request);
  }

  stopOrder(id: string, reason: string, rowVersion: number): Observable<MedicationOrder> {
    return this.post(`${this.api}/medication-orders/${id}/stop`, { reason, rowVersion });
  }

  recordVitals(admissionId: string, request: VitalSignRequest): Observable<VitalSign> {
    return this.post(`${this.api}/ipd/admissions/${admissionId}/vitals`, request);
  }

  vitalsInError(id: string, reason: string): Observable<VitalSign> {
    return this.post(`${this.api}/vital-signs/${id}/entered-in-error`, { reason });
  }

  // Nursing module
  currentShift(): Observable<CurrentShift> {
    return this.get(`${this.api}/nursing/current-shift`);
  }

  ward(wardId: string): Observable<WardRound> {
    return this.get(`${this.api}/nursing/wards/${wardId}`);
  }

  patient(admissionId: string): Observable<NursingPatient> {
    return this.get(`${this.api}/nursing/admissions/${admissionId}`);
  }

  mar(admissionId: string, date: string | null): Observable<Mar> {
    return this.get(`${this.api}/nursing/admissions/${admissionId}/mar`, date ? { date } : {});
  }

  recordDose(admissionId: string, request: AdministrationRequest): Observable<Administration> {
    return this.post(`${this.api}/nursing/admissions/${admissionId}/mar`, request);
  }

  doseInError(id: string, reason: string): Observable<Administration> {
    return this.post(`${this.api}/nursing/mar/${id}/entered-in-error`, { reason });
  }

  fluids(admissionId: string, date: string | null): Observable<FluidDay> {
    return this.get(`${this.api}/nursing/admissions/${admissionId}/intake-output`, date ? { date } : {});
  }

  recordFluid(admissionId: string, request: { measuredAt: string | null; direction: FluidDirection; category: string; volumeMl: number; notes: string | null }): Observable<FluidEntry> {
    return this.post(`${this.api}/nursing/admissions/${admissionId}/intake-output`, request);
  }

  fluidInError(id: string, reason: string): Observable<FluidEntry> {
    return this.post(`${this.api}/nursing/intake-output/${id}/entered-in-error`, { reason });
  }

  notes(admissionId: string): Observable<{ patient: NursingPatient; notes: NursingNote[] }> {
    return this.get(`${this.api}/nursing/admissions/${admissionId}/notes`);
  }

  addNote(admissionId: string, kind: NursingNoteKind, text: string): Observable<NursingNote> {
    return this.post(`${this.api}/nursing/admissions/${admissionId}/notes`, { kind, text });
  }

  noteInError(id: string, reason: string): Observable<NursingNote> {
    return this.post(`${this.api}/nursing/notes/${id}/entered-in-error`, { reason });
  }

  handovers(wardId: string, date: string | null): Observable<Handover[]> {
    return this.get(`${this.api}/nursing/handovers`, { wardId, ...(date ? { date } : {}) });
  }

  saveHandover(request: HandoverRequest): Observable<Handover> {
    return this.post(`${this.api}/nursing/handovers`, request);
  }

  receiveHandover(id: string, rowVersion: number): Observable<Handover> {
    return this.post(`${this.api}/nursing/handovers/${id}/receive`, { rowVersion });
  }

  roster(from: string, to: string, wardId?: string): Observable<RosterEntry[]> {
    return this.get(`${this.api}/nursing/roster`, { from, to, ...(wardId ? { wardId } : {}) });
  }

  addRoster(request: { wardId: string; date: string; shift: Shift; employeeId: string; isInCharge: boolean }): Observable<RosterEntry> {
    return this.post(`${this.api}/nursing/roster`, request);
  }

  removeRoster(id: string): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(`${this.api}/nursing/roster/${id}`).pipe(map(() => undefined));
  }

  private get<T>(url: string, query: Record<string, string> = {}): Observable<T> {
    return this.http.get<ApiResponse<T>>(url, { params: new HttpParams({ fromObject: query }) }).pipe(map((r) => r.data!));
  }

  private post<T>(url: string, body: unknown): Observable<T> {
    return this.http.post<ApiResponse<T>>(url, body).pipe(map((r) => r.data!));
  }
}

/** Age in years from a "yyyy-MM-dd" date of birth. */
export function ageOf(dateOfBirth: string | null): string {
  if (!dateOfBirth) {
    return '';
  }
  const dob = new Date(dateOfBirth);
  const now = new Date();
  let years = now.getFullYear() - dob.getFullYear();
  if (now < new Date(now.getFullYear(), dob.getMonth(), dob.getDate())) {
    years--;
  }
  return `${years}y`;
}

/** Value for an `<input type="datetime-local">` in the browser's (hospital) time. */
export function toLocalInput(value: Date | string): string {
  const d = typeof value === 'string' ? new Date(value) : value;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** UTC ISO string from an `<input type="datetime-local">` value, or null when empty. */
export function fromLocalInput(value: string | null | undefined): string | null {
  return value ? new Date(value).toISOString() : null;
}
