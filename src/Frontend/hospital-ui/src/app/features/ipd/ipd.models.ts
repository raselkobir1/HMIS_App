import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse, PaginationQuery } from '../../core/models/api-response.model';

export const IpdPermissions = {
  view: 'Ipd.View',
  setupManage: 'Ipd.Setup.Manage',
  housekeeping: 'Ipd.Beds.Housekeeping',
  admit: 'Ipd.Admit',
  dischargeOrder: 'Ipd.Discharge.Order',
  dischargeComplete: 'Ipd.Discharge.Complete',
  dischargeOverride: 'Ipd.Discharge.Override',
  dischargeReopen: 'Ipd.Discharge.Reopen',
  clinicalView: 'Clinical.View',
} as const;

export type WardType = 'General' | 'Cabin' | 'Icu' | 'Hdu' | 'Nicu' | 'Maternity' | 'Other';
export type BedStatus = 'Available' | 'Occupied' | 'Reserved' | 'UnderMaintenance' | 'CleaningRequired';
export type AdmissionStatus = 'Admitted' | 'DischargeOrdered' | 'FinalBilled' | 'Discharged';
export type AdmissionSource = 'Direct' | 'Opd' | 'Emergency' | 'Referral';
export type DischargeType = 'Normal' | 'OnRequest' | 'LeftAgainstMedicalAdvice' | 'Referred' | 'Death';

export const WARD_TYPE_LABELS: Record<WardType, string> = {
  General: 'General ward',
  Cabin: 'Cabin',
  Icu: 'ICU',
  Hdu: 'HDU',
  Nicu: 'NICU',
  Maternity: 'Maternity',
  Other: 'Other',
};
export const BED_STATUS_LABELS: Record<BedStatus, string> = {
  Available: 'Available',
  Occupied: 'Occupied',
  Reserved: 'Reserved',
  UnderMaintenance: 'Under maintenance',
  CleaningRequired: 'Cleaning required',
};
export const ADMISSION_STATUS_LABELS: Record<AdmissionStatus, string> = {
  Admitted: 'Admitted',
  DischargeOrdered: 'Discharge ordered',
  FinalBilled: 'Final bill ready',
  Discharged: 'Discharged',
};
export const SOURCE_LABELS: Record<AdmissionSource, string> = { Direct: 'Direct', Opd: 'From OPD', Emergency: 'Emergency', Referral: 'Referral' };
export const DISCHARGE_TYPE_LABELS: Record<DischargeType, string> = {
  Normal: 'Normal discharge',
  OnRequest: 'Discharge on request',
  LeftAgainstMedicalAdvice: 'Left against medical advice (LAMA)',
  Referred: 'Referred to another facility',
  Death: 'Death',
};

export interface BedCategory {
  id: string;
  name: string;
  dailyRate: number;
  suggestedDeposit: number;
  isActive: boolean;
  rowVersion: number;
}

export interface Ward {
  id: string;
  code: string;
  name: string;
  type: WardType;
  departmentId: string | null;
  departmentName: string | null;
  building: string | null;
  floor: string | null;
  isActive: boolean;
  bedCount: number;
  rowVersion: number;
}

export interface Bed {
  id: string;
  wardId: string;
  wardName: string | null;
  bedNumber: string;
  room: string | null;
  categoryId: string;
  categoryName: string | null;
  dailyRate: number;
  status: BedStatus;
  statusNote: string | null;
  isActive: boolean;
  rowVersion: number;
}

export interface Occupant {
  admissionId: string;
  admissionNumber: string;
  patientId: string;
  patientName: string | null;
  patientMRNumber: string | null;
  patientGender: string | null;
  doctorName: string | null;
  admittedAt: string;
  status: AdmissionStatus;
}

export interface BoardBed {
  id: string;
  bedNumber: string;
  room: string | null;
  categoryName: string | null;
  dailyRate: number;
  suggestedDeposit: number;
  status: BedStatus;
  statusNote: string | null;
  isActive: boolean;
  occupant: Occupant | null;
}

export interface WardBoard {
  id: string;
  code: string;
  name: string;
  type: WardType;
  building: string | null;
  floor: string | null;
  available: number;
  occupied: number;
  other: number;
  beds: BoardBed[];
}

export interface AdmissionSummary {
  id: string;
  admissionNumber: string;
  patientId: string;
  patientMRNumber: string | null;
  patientName: string | null;
  patientGender: string | null;
  patientDateOfBirth: string | null;
  doctorId: string;
  doctorName: string | null;
  wardName: string | null;
  bedNumber: string | null;
  admittedAt: string;
  dischargedAt: string | null;
  days: number;
  status: AdmissionStatus;
}

export interface BedStay {
  bedId: string;
  wardName: string | null;
  bedNumber: string | null;
  room: string | null;
  categoryName: string | null;
  startedAt: string;
  endedAt: string | null;
  reason: string;
}

export interface DischargeInfo {
  type: DischargeType;
  finalDiagnosis: string | null;
  hospitalCourse: string | null;
  dischargeMedications: string | null;
  advice: string | null;
  followUpDate: string | null;
  orderedByName: string | null;
  orderedAt: string | null;
}

export interface BillInfo {
  invoiceId: string;
  invoiceNumber: string;
  status: 'Draft' | 'Finalized' | 'Cancelled';
  subtotal: number;
  netAmount: number;
  paidAmount: number;
  dueAmount: number;
}

export interface Admission {
  summary: AdmissionSummary;
  departmentName: string | null;
  source: AdmissionSource;
  sourceReference: string | null;
  reason: string;
  emergencyContactName: string | null;
  emergencyContactRelation: string | null;
  emergencyContactPhone: string | null;
  currentBedId: string | null;
  currentCategoryName: string | null;
  currentDailyRate: number;
  suggestedDeposit: number;
  bedHistory: BedStay[];
  dischargeOrdered: boolean;
  discharge: DischargeInfo | null;
  releasedAt: string | null;
  releasedWithDue: number | null;
  releaseOverrideReason: string | null;
  /** Set when an administrator reopened the stay after the final bill. */
  reopenedAt: string | null;
  reopenReason: string | null;
  bill: BillInfo | null;
  advanceBalance: number;
  rowVersion: number;
}

export interface AdmitRequest {
  patientId: string;
  attendingDoctorId: string;
  bedId: string;
  source: AdmissionSource;
  sourceReference: string | null;
  reason: string;
  emergencyContactName: string | null;
  emergencyContactRelation: string | null;
  emergencyContactPhone: string | null;
}

export interface DischargeOrderRequest {
  type: DischargeType;
  finalDiagnosis: string;
  hospitalCourse: string | null;
  dischargeMedications: string | null;
  advice: string | null;
  followUpDate: string | null;
  rowVersion: number;
}

export interface AdmissionQuery extends PaginationQuery {
  currentOnly?: boolean;
  status?: AdmissionStatus;
  wardId?: string;
  doctorId?: string;
  patientId?: string;
}

export interface DischargeSummary {
  admissionNumber: string;
  patient: { id: string; mrNumber: string; fullName: string; gender: string; dateOfBirth: string | null; mobileNumber: string } | null;
  doctor: { id: string; fullName: string; qualifications: string; specialization: string; registrationNumber: string } | null;
  departmentName: string | null;
  wardName: string | null;
  bedNumber: string | null;
  admittedAt: string;
  /** The discharge (order) time; bed charges stop here. */
  dischargedAt: string | null;
  /** When the patient actually left the ward. */
  releasedAt: string | null;
  reason: string;
  discharge: DischargeInfo;
}

@Injectable({ providedIn: 'root' })
export class IpdApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/ipd`;

  categories(): Observable<BedCategory[]> {
    return this.get('/bed-categories', {});
  }

  saveCategory(id: string | null, body: Omit<BedCategory, 'id' | 'isActive' | 'rowVersion'> & { rowVersion?: number }): Observable<BedCategory> {
    return id ? this.send('put', `/bed-categories/${id}`, body) : this.send('post', '/bed-categories', body);
  }

  setCategoryActive(id: string, isActive: boolean): Observable<BedCategory> {
    return this.send('patch', `/bed-categories/${id}/active`, { isActive });
  }

  wards(): Observable<Ward[]> {
    return this.get('/wards', {});
  }

  saveWard(id: string | null, body: object): Observable<Ward> {
    return id ? this.send('put', `/wards/${id}`, body) : this.send('post', '/wards', body);
  }

  setWardActive(id: string, isActive: boolean): Observable<Ward> {
    return this.send('patch', `/wards/${id}/active`, { isActive });
  }

  beds(query: { wardId?: string; assignableOnly?: boolean }): Observable<Bed[]> {
    return this.get('/beds', query);
  }

  saveBed(id: string | null, body: object): Observable<Bed> {
    return id ? this.send('put', `/beds/${id}`, body) : this.send('post', '/beds', body);
  }

  setBedActive(id: string, isActive: boolean): Observable<Bed> {
    return this.send('patch', `/beds/${id}/active`, { isActive });
  }

  setBedStatus(id: string, status: BedStatus, note: string | null): Observable<Bed> {
    return this.send('patch', `/beds/${id}/status`, { status, note });
  }

  board(): Observable<WardBoard[]> {
    return this.get('/bed-board', {});
  }

  admissions(query: AdmissionQuery): Observable<PagedResponse<AdmissionSummary>> {
    return this.get('/admissions', query);
  }

  admission(id: string): Observable<Admission> {
    return this.get(`/admissions/${id}`, {});
  }

  admit(request: AdmitRequest): Observable<Admission> {
    return this.send('post', '/admissions', request);
  }

  transfer(id: string, bedId: string, reason: string, rowVersion: number): Observable<Admission> {
    return this.send('post', `/admissions/${id}/transfer`, { bedId, reason, rowVersion });
  }

  changeDoctor(id: string, doctorId: string, rowVersion: number): Observable<Admission> {
    return this.send('post', `/admissions/${id}/doctor`, { doctorId, rowVersion });
  }

  syncCharges(id: string): Observable<Admission> {
    return this.send('post', `/admissions/${id}/charges/sync`, {});
  }

  orderDischarge(id: string, request: DischargeOrderRequest): Observable<Admission> {
    return this.send('put', `/admissions/${id}/discharge-order`, request);
  }

  cancelDischargeOrder(id: string, rowVersion: number): Observable<Admission> {
    return this.send('post', `/admissions/${id}/discharge-order/cancel`, { rowVersion });
  }

  prepareFinalBill(id: string, rowVersion: number): Observable<Admission> {
    return this.send('post', `/admissions/${id}/final-bill`, { rowVersion });
  }

  /** Administrator's correction after the final bill: the patient stays (Ipd.Discharge.Reopen). */
  reopen(id: string, reason: string, rowVersion: number): Observable<Admission> {
    return this.send('post', `/admissions/${id}/reopen`, { reason, rowVersion });
  }

  release(id: string, overrideReason: string | null, rowVersion: number): Observable<Admission> {
    return this.send('post', `/admissions/${id}/release`, { overrideReason, rowVersion });
  }

  dischargeSummary(id: string): Observable<DischargeSummary> {
    return this.get(`/admissions/${id}/discharge-summary`, {});
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

/** Beds that can take a patient, grouped for a select (ward → beds). */
export function groupBeds(beds: Bed[]): { ward: string; beds: Bed[] }[] {
  const groups = new Map<string, Bed[]>();
  for (const bed of beds) {
    const key = bed.wardName ?? '';
    groups.set(key, [...(groups.get(key) ?? []), bed]);
  }
  return [...groups.entries()].map(([ward, list]) => ({ ward, beds: list }));
}
