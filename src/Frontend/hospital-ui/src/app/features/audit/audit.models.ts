import { PaginationQuery } from '../../core/models/api-response.model';

export const AuditPermissions = { view: 'Audit.View' } as const;

export interface AuditLogListItem {
  id: string;
  timestamp: string;
  userId: string | null;
  userName: string | null;
  action: string;
  module: string;
  entityName: string;
  entityId: string;
  ipAddress: string | null;
}

export interface AuditLogDetail extends AuditLogListItem {
  traceId: string | null;
  oldValues: string | null;
  newValues: string | null;
}

export interface AuditFilterOptions {
  modules: string[];
  entityNames: string[];
  actions: string[];
}

export interface AuditLogQuery extends PaginationQuery {
  fromUtc?: string;
  toUtc?: string;
  module?: string;
  entityName?: string;
  entityId?: string;
  action?: string;
}
