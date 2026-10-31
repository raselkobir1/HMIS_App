import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse } from '../../core/models/api-response.model';
import { EmployeeDetail, EmployeeListItem, EmployeeLookupItem, EmployeeQuery, EmployeeRequest, EmploymentStatus } from './hr.models';

@Injectable({ providedIn: 'root' })
export class HrApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/employees`;

  search(query: EmployeeQuery): Observable<PagedResponse<EmployeeListItem>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<ApiResponse<PagedResponse<EmployeeListItem>>>(this.base, { params }).pipe(map((r) => r.data!));
  }

  get(id: string): Observable<EmployeeDetail> {
    return this.http.get<ApiResponse<EmployeeDetail>>(`${this.base}/${id}`).pipe(map((r) => r.data!));
  }

  /** Current employees for pickers. */
  lookup(search: string, take = 20): Observable<EmployeeLookupItem[]> {
    const params = new HttpParams().set('search', search).set('take', take);
    return this.http.get<ApiResponse<EmployeeLookupItem[]>>(`${this.base}/lookup`, { params }).pipe(map((r) => r.data!));
  }

  create(request: EmployeeRequest): Observable<EmployeeDetail> {
    return this.http.post<ApiResponse<EmployeeDetail>>(this.base, request).pipe(map((r) => r.data!));
  }

  update(id: string, request: EmployeeRequest & { rowVersion: number }): Observable<EmployeeDetail> {
    return this.http.put<ApiResponse<EmployeeDetail>>(`${this.base}/${id}`, request).pipe(map((r) => r.data!));
  }

  changeStatus(id: string, status: EmploymentStatus, exitDate: string | null): Observable<EmployeeDetail> {
    return this.http
      .patch<ApiResponse<EmployeeDetail>>(`${this.base}/${id}/status`, { status, exitDate })
      .pipe(map((r) => r.data!));
  }
}
