import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

import { AuditLogDetail } from './audit.models';

interface ChangeRow {
  field: string;
  oldValue: string;
  newValue: string;
  changed: boolean;
}

/** Shows one audit record with a field-by-field before/after comparison. */
@Component({
  selector: 'app-audit-detail-dialog',
  imports: [DatePipe, MatDialogModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ log.action }} · {{ log.entityName }}</h2>
    <mat-dialog-content>
      <dl class="meta">
        <dt>When</dt>
        <dd>{{ log.timestamp | date: 'dd MMM yyyy, h:mm:ss a' }}</dd>
        <dt>By</dt>
        <dd>{{ log.userName ?? 'System' }}</dd>
        <dt>Module</dt>
        <dd>{{ log.module }}</dd>
        <dt>Record ID</dt>
        <dd class="mono">{{ log.entityId }}</dd>
        <dt>IP address</dt>
        <dd>{{ log.ipAddress ?? '—' }}</dd>
        <dt>Trace ID</dt>
        <dd class="mono">{{ log.traceId ?? '—' }}</dd>
      </dl>

      @if (rows().length) {
        <table class="changes">
          <thead>
            <tr>
              <th>Field</th>
              @if (hasOld()) {
                <th>Before</th>
              }
              @if (hasNew()) {
                <th>After</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.field) {
              <tr [class.changed]="row.changed && hasOld() && hasNew()">
                <td class="field">{{ row.field }}</td>
                @if (hasOld()) {
                  <td>{{ row.oldValue }}</td>
                }
                @if (hasNew()) {
                  <td>{{ row.newValue }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      } @else {
        <p class="empty">No field values were recorded for this event.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .meta {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 4px 16px;
      margin: 0 0 16px;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      word-break: break-all;
    }
    .mono {
      font-family: ui-monospace, Consolas, monospace;
      font-size: 12px;
    }
    .changes {
      width: 100%;
      border-collapse: collapse;
    }
    .changes th,
    .changes td {
      text-align: left;
      padding: 6px 8px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      vertical-align: top;
      word-break: break-word;
    }
    .field {
      font-weight: 500;
      white-space: nowrap;
    }
    tr.changed td {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .empty {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class AuditDetailDialog {
  protected readonly log = inject<AuditLogDetail>(MAT_DIALOG_DATA);

  private readonly oldValues = parse(this.log.oldValues);
  private readonly newValues = parse(this.log.newValues);

  protected readonly hasOld = computed(() => this.oldValues !== null);
  protected readonly hasNew = computed(() => this.newValues !== null);

  protected readonly rows = computed<ChangeRow[]>(() => {
    const fields = new Set([...Object.keys(this.oldValues ?? {}), ...Object.keys(this.newValues ?? {})]);
    return [...fields].map((field) => {
      const oldValue = format(this.oldValues?.[field]);
      const newValue = format(this.newValues?.[field]);
      return { field, oldValue, newValue, changed: oldValue !== newValue };
    });
  });
}

function parse(json: string | null): Record<string, unknown> | null {
  if (!json) {
    return null;
  }
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return { value: json };
  }
}

function format(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (Array.isArray(value)) return value.length ? value.join(', ') : '(none)';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}
