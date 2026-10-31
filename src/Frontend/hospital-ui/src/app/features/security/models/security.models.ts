import { PaginationQuery } from '../../../core/models/api-response.model';

export const SecurityPermissions = {
  usersView: 'Identity.Users.View',
  usersManage: 'Identity.Users.Manage',
  rolesView: 'Identity.Roles.View',
  rolesManage: 'Identity.Roles.Manage',
  loginHistoryView: 'Identity.LoginHistory.View',
} as const;

export interface RoleReference {
  id: string;
  name: string;
}

export interface UserListItem {
  id: string;
  userName: string;
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  isActive: boolean;
  isLockedOut: boolean;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
  roles: string[];
}

export interface UserDetail {
  id: string;
  userName: string;
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  employeeId: string | null;
  isActive: boolean;
  isLockedOut: boolean;
  lockoutEnd: string | null;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  roles: RoleReference[];
  directPermissionIds: string[];
  rowVersion: number;
}

export interface UserListQuery extends PaginationQuery {
  isActive?: boolean;
  roleId?: string;
}

export interface UserCreateRequest {
  userName: string;
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  password: string;
  roleIds: string[];
  employeeId: string | null;
}

export interface UserUpdateRequest {
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  employeeId: string | null;
  rowVersion: number;
}

export interface RoleListItem {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  userCount: number;
  permissionCount: number;
}

export interface RoleDetail {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  permissionIds: string[];
  rowVersion: number;
}

export interface Permission {
  id: string;
  code: string;
  module: string;
  description: string;
  requiresExplicitGrant: boolean;
}

export interface PermissionGroup {
  module: string;
  permissions: Permission[];
}

export interface LoginHistoryEntry {
  id: string;
  userId: string | null;
  userNameAttempted: string;
  succeeded: boolean;
  failureReason: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  attemptedAt: string;
}

export interface LoginHistoryQuery extends PaginationQuery {
  succeeded?: boolean;
  fromUtc?: string;
  toUtc?: string;
}
