import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { PermissionGroup } from '../models/security.models';

/** Permission catalogue grouped by module with per-module "select all". Two-way binds the selected ids. */
@Component({
  selector: 'app-permission-checklist',
  imports: [MatCheckboxModule, MatIconModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (group of groups(); track group.module) {
      <fieldset class="group">
        <legend>
          <mat-checkbox
            [checked]="allSelected(group)"
            [indeterminate]="someSelected(group)"
            [disabled]="disabled()"
            (change)="toggleGroup(group, $event.checked)"
          >
            {{ group.module }}
          </mat-checkbox>
        </legend>
        <div class="items">
          @for (permission of group.permissions; track permission.id) {
            <mat-checkbox
              [checked]="selected().includes(permission.id)"
              [disabled]="disabled()"
              (change)="toggle(permission.id, $event.checked)"
            >
              <span class="label">
                {{ permission.description }}
                @if (permission.requiresExplicitGrant) {
                  <mat-icon class="sensitive" matTooltip="Sensitive clinical permission — grant deliberately">shield</mat-icon>
                }
              </span>
              <span class="code">{{ permission.code }}</span>
            </mat-checkbox>
          }
        </div>
      </fieldset>
    } @empty {
      <p class="empty">No permissions available.</p>
    }
  `,
  styles: `
    .group {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 8px;
      margin: 0 0 12px;
      padding: 4px 12px 8px;
    }
    legend {
      font: var(--mat-sys-title-small);
      padding: 0 4px;
    }
    .items {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    }
    .label {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .code {
      display: block;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .sensitive {
      font-size: 16px;
      width: 16px;
      height: 16px;
      color: var(--mat-sys-error);
    }
    .empty {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class PermissionChecklist {
  readonly groups = input.required<PermissionGroup[]>();
  readonly selected = model.required<string[]>();
  readonly disabled = input(false);

  protected allSelected(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.selected().includes(p.id));
  }

  protected someSelected(group: PermissionGroup): boolean {
    const count = group.permissions.filter((p) => this.selected().includes(p.id)).length;
    return count > 0 && count < group.permissions.length;
  }

  protected toggle(id: string, checked: boolean): void {
    this.selected.update((ids) => (checked ? [...new Set([...ids, id])] : ids.filter((x) => x !== id)));
  }

  protected toggleGroup(group: PermissionGroup, checked: boolean): void {
    const groupIds = group.permissions.map((p) => p.id);
    this.selected.update((ids) =>
      checked ? [...new Set([...ids, ...groupIds])] : ids.filter((id) => !groupIds.includes(id)),
    );
  }
}
