import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse } from '../../core/models/api-response.model';
import { AuditFilterOptions, AuditLogDetail, AuditLogListItem, AuditLogQuery } from './audit.models';

/** Read-only audit log API. */
@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/audit-logs`;

  search(query: AuditLogQuery): Observable<PagedResponse<AuditLogListItem>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<ApiResponse<PagedResponse<AuditLogListItem>>>(this.base, { params }).pipe(map((r) => r.data!));
  }

  get(id: string): Observable<AuditLogDetail> {
    return this.http.get<ApiResponse<AuditLogDetail>>(`${this.base}/${id}`).pipe(map((r) => r.data!));
  }

  filterOptions(): Observable<AuditFilterOptions> {
    return this.http.get<ApiResponse<AuditFilterOptions>>(`${this.base}/filter-options`).pipe(map((r) => r.data!));
  }
}
