import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse, PaginationQuery } from '../../core/models/api-response.model';

export const DoctorPermissions = { view: 'HR.Doctors.View', manage: 'HR.Doctors.Manage' } as const;

export interface Doctor {
  id: string;
  employeeId: string;
  employeeCode: string;
  fullName: string;
  departmentId: string;
  departmentName: string | null;
  designationName: string | null;
  specialization: string;
  qualifications: string;
  registrationNumber: string;
  newPatientFee: number;
  followUpFee: number;
  inpatientVisitFee: number;
  isAcceptingAppointments: boolean;
  employmentStatus: string;
  rowVersion: number;
}

export interface DoctorQuery extends PaginationQuery {
  departmentId?: string;
  isAcceptingAppointments?: boolean;
}

export interface DoctorRequest {
  specialization: string;
  qualifications: string;
  registrationNumber: string;
  newPatientFee: number;
  followUpFee: number;
  /** Charged once per day when the doctor signs a ward-round or consultant-review note for an inpatient. */
  inpatientVisitFee: number;
}

@Injectable({ providedIn: 'root' })
export class DoctorApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/doctors`;

  search(query: DoctorQuery): Observable<PagedResponse<Doctor>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<ApiResponse<PagedResponse<Doctor>>>(this.base, { params }).pipe(map((r) => r.data!));
  }

  /** Doctors who currently take appointments (for booking pickers). */
  bookable(departmentId?: string): Observable<Doctor[]> {
    return this.search({ pageNumber: 1, pageSize: 100, isAcceptingAppointments: true, departmentId }).pipe(
      map((page) => page.items.filter((d) => d.employmentStatus === 'Active' || d.employmentStatus === 'OnLeave')),
    );
  }

  create(request: DoctorRequest & { employeeId: string }): Observable<Doctor> {
    return this.http.post<ApiResponse<Doctor>>(this.base, request).pipe(map((r) => r.data!));
  }

  update(id: string, request: DoctorRequest & { isAcceptingAppointments: boolean; rowVersion: number }): Observable<Doctor> {
    return this.http.put<ApiResponse<Doctor>>(`${this.base}/${id}`, request).pipe(map((r) => r.data!));
  }
}
