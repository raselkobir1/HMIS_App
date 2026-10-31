import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';

export const ClinicalPermissions = {
  view: 'Clinical.View',
  vitalsRecord: 'Clinical.Vitals.Record',
  consultationManage: 'Clinical.Consultation.Manage',
} as const;

export type ConsultationStatus = 'Draft' | 'Finalized';
export type DiagnosisType = 'Primary' | 'Secondary';
export type MedicineTiming = 'AfterFood' | 'BeforeFood' | 'WithFood' | 'AnyTime';
export type DurationUnit = 'Days' | 'Weeks' | 'Months' | 'Continue';

export const TIMING_LABELS: Record<MedicineTiming, string> = {
  AfterFood: 'After food',
  BeforeFood: 'Before food',
  WithFood: 'With food',
  AnyTime: 'Any time',
};

export const DURATION_UNITS: DurationUnit[] = ['Days', 'Weeks', 'Months', 'Continue'];
export const MEDICINE_FORMS = ['Tab', 'Cap', 'Syrup', 'Susp', 'Inj', 'Drop', 'Cream', 'Oint', 'Gel', 'Inhaler', 'Spray', 'Supp', 'Sachet'];
export const ROUTES = ['Oral', 'IV', 'IM', 'SC', 'Topical', 'Inhalation', 'Nasal', 'Eye', 'Ear', 'Rectal', 'Sublingual'];

export interface VitalSign {
  id: string;
  recordedAt: string;
  recordedBy: string | null;
  systolic: number | null;
  diastolic: number | null;
  pulse: number | null;
  temperatureF: number | null;
  respiratoryRate: number | null;
  spO2: number | null;
  weightKg: number | null;
  heightCm: number | null;
  bmi: number | null;
  notes: string | null;
  isEnteredInError: boolean;
  errorReason: string | null;
}

export type VitalSignRequest = Omit<VitalSign, 'id' | 'recordedAt' | 'recordedBy' | 'bmi' | 'isEnteredInError' | 'errorReason'>;

export interface Diagnosis {
  id: string | null;
  type: DiagnosisType;
  description: string;
}

export interface PrescriptionItem {
  id: string | null;
  medicineName: string;
  form: string | null;
  strength: string | null;
  route: string | null;
  dosePattern: string;
  timing: MedicineTiming;
  durationValue: number | null;
  durationUnit: DurationUnit;
  quantity: number | null;
  instructions: string | null;
}

export interface Investigation {
  id: string | null;
  name: string;
  notes: string | null;
}

export interface Addendum {
  id: string;
  text: string;
  createdAt: string;
  createdBy: string | null;
}

export interface Consultation {
  id: string;
  encounterType: string;
  encounterId: string;
  encounterNumber: string;
  encounterDate: string;
  patientId: string;
  doctorId: string;
  status: ConsultationStatus;
  chiefComplaint: string | null;
  historyOfPresentIllness: string | null;
  examination: string | null;
  advice: string | null;
  followUpDate: string | null;
  followUpInstructions: string | null;
  referralTo: string | null;
  referralReason: string | null;
  diagnoses: Diagnosis[];
  prescription: PrescriptionItem[];
  investigations: Investigation[];
  addenda: Addendum[];
  finalizedAt: string | null;
  rowVersion: number;
}

export type ConsultationSave = Omit<
  Consultation,
  'id' | 'encounterType' | 'encounterId' | 'encounterNumber' | 'encounterDate' | 'patientId' | 'doctorId' | 'status' | 'addenda' | 'finalizedAt'
>;

export interface PatientInfo {
  id: string;
  mrNumber: string;
  fullName: string;
  gender: string;
  dateOfBirth: string | null;
  mobileNumber: string;
}

export interface DoctorInfo {
  id: string;
  fullName: string;
  specialization: string;
  qualifications: string;
  registrationNumber: string;
}

export interface AllergyAlert {
  allergen: string;
  type: string;
  severity: string;
  reaction: string | null;
}

export interface HistoryItem {
  category: string;
  description: string;
  onsetDate: string | null;
}

export interface ConsultationSummary {
  id: string;
  encounterType: string;
  encounterNumber: string;
  encounterDate: string;
  doctorId: string;
  doctorName: string | null;
  status: ConsultationStatus;
  primaryDiagnosis: string | null;
  medicineCount: number;
}

export interface OpdWorkspace {
  encounter: { id: string; number: string; date: string; serialNumber: number; visitType: string; status: string };
  patient: PatientInfo;
  doctor: DoctorInfo | null;
  allergies: AllergyAlert[];
  history: HistoryItem[];
  vitals: VitalSign[];
  consultation: Consultation | null;
  previousConsultations: ConsultationSummary[];
  canWrite: boolean;
}

export interface ConsultationRecord {
  consultation: Consultation;
  patient: PatientInfo;
  doctor: DoctorInfo | null;
  allergies: AllergyAlert[];
  latestVitals: VitalSign | null;
  canWrite: boolean;
}

/** "BP 130/85 · Pulse 82 · Temp 99.1°F …" */
export function vitalsSummary(v: VitalSign): string {
  const parts: string[] = [];
  if (v.systolic !== null) parts.push(`BP ${v.systolic}/${v.diastolic} mmHg`);
  if (v.pulse !== null) parts.push(`Pulse ${v.pulse}/min`);
  if (v.temperatureF !== null) parts.push(`Temp ${v.temperatureF}°F`);
  if (v.respiratoryRate !== null) parts.push(`RR ${v.respiratoryRate}/min`);
  if (v.spO2 !== null) parts.push(`SpO₂ ${v.spO2}%`);
  if (v.weightKg !== null) parts.push(`Wt ${v.weightKg} kg`);
  if (v.heightCm !== null) parts.push(`Ht ${v.heightCm} cm`);
  if (v.bmi !== null) parts.push(`BMI ${v.bmi}`);
  return parts.join(' · ');
}

/** "7 days", "2 weeks", "Continue" */
export function durationLabel(item: PrescriptionItem): string {
  if (item.durationUnit === 'Continue') return 'Continue';
  const unit = item.durationUnit.toLowerCase();
  return `${item.durationValue} ${item.durationValue === 1 ? unit.slice(0, -1) : unit}`;
}

@Injectable({ providedIn: 'root' })
export class ClinicalApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  workspace(encounterId: string): Observable<OpdWorkspace> {
    return this.unwrap(this.http.get<ApiResponse<OpdWorkspace>>(`${this.base}/opd/encounters/${encounterId}`));
  }

  recordVitals(encounterId: string, request: VitalSignRequest): Observable<VitalSign> {
    return this.unwrap(this.http.post<ApiResponse<VitalSign>>(`${this.base}/opd/encounters/${encounterId}/vitals`, request));
  }

  vitalsInError(id: string, reason: string): Observable<VitalSign> {
    return this.unwrap(this.http.post<ApiResponse<VitalSign>>(`${this.base}/vital-signs/${id}/entered-in-error`, { reason }));
  }

  openConsultation(encounterId: string): Observable<Consultation> {
    return this.unwrap(this.http.post<ApiResponse<Consultation>>(`${this.base}/opd/encounters/${encounterId}/consultation`, {}));
  }

  consultation(id: string): Observable<ConsultationRecord> {
    return this.unwrap(this.http.get<ApiResponse<ConsultationRecord>>(`${this.base}/consultations/${id}`));
  }

  save(id: string, request: ConsultationSave): Observable<Consultation> {
    return this.unwrap(this.http.put<ApiResponse<Consultation>>(`${this.base}/consultations/${id}`, request));
  }

  finalize(id: string, rowVersion: number): Observable<Consultation> {
    return this.unwrap(this.http.post<ApiResponse<Consultation>>(`${this.base}/consultations/${id}/finalize`, { rowVersion }));
  }

  addAddendum(id: string, text: string): Observable<Consultation> {
    return this.unwrap(this.http.post<ApiResponse<Consultation>>(`${this.base}/consultations/${id}/addenda`, { text }));
  }

  patientHistory(patientId: string): Observable<ConsultationSummary[]> {
    return this.unwrap(this.http.get<ApiResponse<ConsultationSummary[]>>(`${this.base}/patients/${patientId}/consultations`));
  }

  private unwrap<T>(request: Observable<ApiResponse<T>>): Observable<T> {
    return request.pipe(map((r) => r.data as T));
  }
}
