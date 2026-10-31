import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse } from '../../core/models/api-response.model';
import {
  Department,
  DepartmentLookupItem,
  DepartmentQuery,
  DepartmentType,
  Designation,
  DesignationCategory,
  DesignationQuery,
  HospitalProfile,
  HospitalProfileUpdate,
  NumberSequence,
  ResetPolicy,
  SystemSettings,
  YearFormat,
} from './administration.models';

/** Administration API: hospital profile, settings, departments, designations and number sequences. */
@Injectable({ providedIn: 'root' })
export class AdministrationApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getProfile(): Observable<HospitalProfile> {
    return this.get<HospitalProfile>('/hospital-profile');
  }

  updateProfile(request: HospitalProfileUpdate): Observable<HospitalProfile> {
    return this.send<HospitalProfile>('put', '/hospital-profile', request);
  }

  uploadLogo(file: File): Observable<HospitalProfile> {
    const form = new FormData();
    form.append('file', file);
    return this.send<HospitalProfile>('post', '/hospital-profile/logo', form);
  }

  removeLogo(): Observable<HospitalProfile> {
    return this.http.delete<ApiResponse<HospitalProfile>>(`${this.base}/hospital-profile/logo`).pipe(map((r) => r.data!));
  }

  getSettings(): Observable<SystemSettings> {
    return this.get<SystemSettings>('/system-settings');
  }

  updateSettings(request: Omit<SystemSettings, 'timeZone'>): Observable<SystemSettings> {
    return this.send<SystemSettings>('put', '/system-settings', request);
  }

  searchDepartments(query: DepartmentQuery): Observable<PagedResponse<Department>> {
    return this.get<PagedResponse<Department>>('/departments', toParams(query));
  }

  departmentLookup(): Observable<DepartmentLookupItem[]> {
    return this.get<DepartmentLookupItem[]>('/departments/lookup');
  }

  createDepartment(request: DepartmentRequest): Observable<Department> {
    return this.send<Department>('post', '/departments', request);
  }

  updateDepartment(id: string, request: DepartmentRequest & { rowVersion: number }): Observable<Department> {
    return this.send<Department>('put', `/departments/${id}`, request);
  }

  setDepartmentStatus(id: string, isActive: boolean): Observable<Department> {
    return this.send<Department>('patch', `/departments/${id}/status`, { isActive });
  }

  searchDesignations(query: DesignationQuery): Observable<PagedResponse<Designation>> {
    return this.get<PagedResponse<Designation>>('/designations', toParams(query));
  }

  createDesignation(request: { name: string; category: DesignationCategory }): Observable<Designation> {
    return this.send<Designation>('post', '/designations', request);
  }

  updateDesignation(id: string, request: { name: string; category: DesignationCategory; rowVersion: number }): Observable<Designation> {
    return this.send<Designation>('put', `/designations/${id}`, request);
  }

  setDesignationStatus(id: string, isActive: boolean): Observable<Designation> {
    return this.send<Designation>('patch', `/designations/${id}/status`, { isActive });
  }

  getSequences(): Observable<NumberSequence[]> {
    return this.get<NumberSequence[]>('/number-sequences');
  }

  updateSequence(
    key: string,
    request: { prefix: string; yearFormat: YearFormat; padLength: number; resetPolicy: ResetPolicy; rowVersion: number },
  ): Observable<NumberSequence> {
    return this.send<NumberSequence>('put', `/number-sequences/${key}`, request);
  }

  setSequenceNextValue(key: string, nextValue: number): Observable<NumberSequence> {
    return this.send<NumberSequence>('put', `/number-sequences/${key}/next-value`, { nextValue });
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}${path}`, { params }).pipe(map((r) => r.data as T));
  }

  private send<T>(method: 'post' | 'put' | 'patch', path: string, body: unknown): Observable<T> {
    return this.http.request<ApiResponse<T>>(method, `${this.base}${path}`, { body }).pipe(map((r) => r.data as T));
  }
}

export interface DepartmentRequest {
  code: string;
  name: string;
  type: DepartmentType;
  parentDepartmentId: string | null;
  description: string | null;
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
