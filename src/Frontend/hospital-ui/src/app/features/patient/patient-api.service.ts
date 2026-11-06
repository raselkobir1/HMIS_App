import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, shareReplay } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse } from '../../core/models/api-response.model';
import {
  Allergy,
  AllergySeverity,
  AllergyStatus,
  AllergyType,
  DocumentCategory,
  DuplicateCandidate,
  HistoryCategory,
  MedicalHistory,
  MedicalInfo,
  MergeRequest,
  MergeStatus,
  Patient,
  PatientDetails,
  PatientDocument,
  PatientListItem,
  PatientSearchQuery,
} from './patient.models';

@Injectable({ providedIn: 'root' })
export class PatientApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/patients`;
  private districts$?: Observable<string[]>;

  search(query: PatientSearchQuery): Observable<PagedResponse<PatientListItem>> {
    return this.get<PagedResponse<PatientListItem>>('', toParams(query));
  }

  duplicates(query: Record<string, string | null | undefined>): Observable<DuplicateCandidate[]> {
    return this.get<DuplicateCandidate[]>('/duplicate-candidates', toParams(query));
  }

  districts(): Observable<string[]> {
    this.districts$ ??= this.get<string[]>('/districts').pipe(shareReplay(1));
    return this.districts$;
  }

  getById(id: string): Observable<Patient> {
    return this.get<Patient>(`/${id}`);
  }

  getByMRNumber(mrNumber: string): Observable<Patient> {
    return this.get<Patient>(`/by-mrn/${encodeURIComponent(mrNumber)}`);
  }

  register(details: PatientDetails): Observable<Patient> {
    return this.send<Patient>('post', '', details);
  }

  update(id: string, details: PatientDetails & { rowVersion: number }): Observable<Patient> {
    return this.send<Patient>('put', `/${id}`, details);
  }

  setStatus(id: string, isActive: boolean, rowVersion: number): Observable<Patient> {
    return this.send<Patient>('patch', `/${id}/status`, { isActive, rowVersion });
  }

  photo(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/photo`, { responseType: 'blob' });
  }

  uploadPhoto(id: string, file: File): Observable<Patient> {
    const form = new FormData();
    form.append('file', file);
    return this.send<Patient>('post', `/${id}/photo`, form);
  }

  removePhoto(id: string): Observable<Patient> {
    return this.http.delete<ApiResponse<Patient>>(`${this.base}/${id}/photo`).pipe(map((r) => r.data!));
  }

  medicalInfo(id: string): Observable<MedicalInfo> {
    return this.get<MedicalInfo>(`/${id}/medical-info`);
  }

  addAllergy(id: string, body: AllergyBody): Observable<Allergy> {
    return this.send<Allergy>('post', `/${id}/allergies`, body);
  }

  updateAllergy(id: string, allergyId: string, body: AllergyBody & { rowVersion: number }): Observable<Allergy> {
    return this.send<Allergy>('put', `/${id}/allergies/${allergyId}`, body);
  }

  setAllergyStatus(id: string, allergyId: string, status: AllergyStatus): Observable<Allergy> {
    return this.send<Allergy>('patch', `/${id}/allergies/${allergyId}/status`, { status });
  }

  addHistory(id: string, body: HistoryBody): Observable<MedicalHistory> {
    return this.send<MedicalHistory>('post', `/${id}/medical-history`, body);
  }

  updateHistory(id: string, historyId: string, body: HistoryBody & { rowVersion: number }): Observable<MedicalHistory> {
    return this.send<MedicalHistory>('put', `/${id}/medical-history/${historyId}`, body);
  }

  markHistoryInError(id: string, historyId: string): Observable<MedicalHistory> {
    return this.send<MedicalHistory>('post', `/${id}/medical-history/${historyId}/entered-in-error`, {});
  }

  documents(id: string): Observable<PatientDocument[]> {
    return this.get<PatientDocument[]>(`/${id}/documents`);
  }

  uploadDocument(id: string, file: File, category: DocumentCategory, title: string): Observable<PatientDocument> {
    const form = new FormData();
    form.append('file', file);
    form.append('category', category);
    form.append('title', title);
    return this.send<PatientDocument>('post', `/${id}/documents`, form);
  }

  documentContent(id: string, documentId: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/documents/${documentId}/content`, { responseType: 'blob' });
  }

  removeDocument(id: string, documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/documents/${documentId}`);
  }

  mergeRequests(status: MergeStatus | undefined, pageNumber: number, pageSize: number): Observable<PagedResponse<MergeRequest>> {
    return this.http
      .get<ApiResponse<PagedResponse<MergeRequest>>>(`${environment.apiBaseUrl}/patient-merge-requests`, {
        params: toParams({ status, pageNumber, pageSize }),
      })
      .pipe(map((r) => r.data!));
  }

  requestMerge(sourcePatientId: string, targetPatientId: string, reason: string): Observable<MergeRequest> {
    return this.http
      .post<ApiResponse<MergeRequest>>(`${environment.apiBaseUrl}/patient-merge-requests`, { sourcePatientId, targetPatientId, reason })
      .pipe(map((r) => r.data!));
  }

  reviewMerge(id: string, approve: boolean, note: string | null): Observable<MergeRequest> {
    return this.http
      .post<ApiResponse<MergeRequest>>(`${environment.apiBaseUrl}/patient-merge-requests/${id}/${approve ? 'approve' : 'reject'}`, { note })
      .pipe(map((r) => r.data!));
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}${path}`, { params }).pipe(map((r) => r.data as T));
  }

  private send<T>(method: 'post' | 'put' | 'patch', path: string, body: unknown): Observable<T> {
    return this.http.request<ApiResponse<T>>(method, `${this.base}${path}`, { body }).pipe(map((r) => r.data as T));
  }
}

export interface AllergyBody {
  allergen: string;
  type: AllergyType;
  severity: AllergySeverity;
  reaction: string | null;
}

export interface HistoryBody {
  category: HistoryCategory;
  description: string;
  onsetDate: string | null;
}

function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
