import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { ApiResponse, PagedResponse } from '../../../core/models/api-response.model';
import {
  LoginHistoryEntry,
  LoginHistoryQuery,
  PermissionGroup,
  RoleDetail,
  RoleListItem,
  UserCreateRequest,
  UserDetail,
  UserListItem,
  UserListQuery,
  UserUpdateRequest,
} from '../models/security.models';

/** Identity API: users, roles, permissions and login history. */
@Injectable({ providedIn: 'root' })
export class SecurityApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  // ---------- Users ----------
  searchUsers(query: UserListQuery): Observable<PagedResponse<UserListItem>> {
    return this.get<PagedResponse<UserListItem>>('/users', toParams(query));
  }

  getUser(id: string): Observable<UserDetail> {
    return this.get<UserDetail>(`/users/${id}`);
  }

  createUser(request: UserCreateRequest): Observable<UserDetail> {
    return this.send<UserDetail>('post', '/users', request);
  }

  updateUser(id: string, request: UserUpdateRequest): Observable<UserDetail> {
    return this.send<UserDetail>('put', `/users/${id}`, request);
  }

  setUserStatus(id: string, isActive: boolean): Observable<UserDetail> {
    return this.send<UserDetail>('patch', `/users/${id}/status`, { isActive });
  }

  resetPassword(id: string, newPassword: string): Observable<unknown> {
    return this.send<unknown>('post', `/users/${id}/reset-password`, { newPassword });
  }

  unlockUser(id: string): Observable<UserDetail> {
    return this.send<UserDetail>('post', `/users/${id}/unlock`, {});
  }

  setUserRoles(id: string, roleIds: string[]): Observable<UserDetail> {
    return this.send<UserDetail>('put', `/users/${id}/roles`, { roleIds });
  }

  setUserPermissions(id: string, permissionIds: string[]): Observable<UserDetail> {
    return this.send<UserDetail>('put', `/users/${id}/permissions`, { permissionIds });
  }

  // ---------- Roles ----------
  searchRoles(query: Partial<PagedQuery> = { pageNumber: 1, pageSize: 100 }): Observable<PagedResponse<RoleListItem>> {
    return this.get<PagedResponse<RoleListItem>>('/roles', toParams(query));
  }

  getRole(id: string): Observable<RoleDetail> {
    return this.get<RoleDetail>(`/roles/${id}`);
  }

  createRole(request: { name: string; description: string | null; permissionIds: string[] }): Observable<RoleDetail> {
    return this.send<RoleDetail>('post', '/roles', request);
  }

  updateRole(id: string, request: { name: string; description: string | null; rowVersion: number }): Observable<RoleDetail> {
    return this.send<RoleDetail>('put', `/roles/${id}`, request);
  }

  deleteRole(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/roles/${id}`);
  }

  setRolePermissions(id: string, permissionIds: string[]): Observable<RoleDetail> {
    return this.send<RoleDetail>('put', `/roles/${id}/permissions`, { permissionIds });
  }

  // ---------- Permissions & history ----------
  getPermissions(): Observable<PermissionGroup[]> {
    return this.get<PermissionGroup[]>('/permissions');
  }

  searchLoginHistory(query: LoginHistoryQuery): Observable<PagedResponse<LoginHistoryEntry>> {
    return this.get<PagedResponse<LoginHistoryEntry>>('/login-history', toParams(query));
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}${path}`, { params }).pipe(map((r) => r.data as T));
  }

  private send<T>(method: 'post' | 'put' | 'patch', path: string, body: unknown): Observable<T> {
    return this.http.request<ApiResponse<T>>(method, `${this.base}${path}`, { body }).pipe(map((r) => r.data as T));
  }
}

type PagedQuery = { pageNumber: number; pageSize: number; search?: string };

/** Builds query params, skipping null/undefined/empty values. */
function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
