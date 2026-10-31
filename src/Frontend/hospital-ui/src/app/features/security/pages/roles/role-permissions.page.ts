import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../../shared/utilities/server-errors';
import { PermissionChecklist } from '../../components/permission-checklist';
import { PermissionGroup, RoleDetail, SecurityPermissions } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';

/** Route: /security/roles/:id/permissions. Read-only for users without Identity.Roles.Manage. */
@Component({
  selector: 'app-role-permissions-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader, PermissionChecklist],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="role() ? 'Permissions: ' + role()!.name : 'Role permissions'" [subtitle]="role()?.description ?? ''">
      <a mat-button routerLink="../..">
        <mat-icon>arrow_back</mat-icon>
        Back to roles
      </a>
      @if (canManage) {
        <button mat-flat-button type="button" [disabled]="saving() || loading() || !dirty()" (click)="save()">Save changes</button>
      }
    </app-page-header>

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    @if (role()) {
      <p class="summary">{{ selected().length }} permission(s) selected</p>
      <app-permission-checklist [groups]="groups()" [(selected)]="selected" [disabled]="!canManage" />
    }
  `,
  styles: `
    .summary {
      color: var(--mat-sys-on-surface-variant);
    }
    .banner {
      padding: 12px;
      margin-bottom: 12px;
      border-radius: 8px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class RolePermissionsPage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(SecurityApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly canManage = inject(AuthService).hasPermission(SecurityPermissions.rolesManage);
  protected readonly role = signal<RoleDetail | null>(null);
  protected readonly groups = signal<PermissionGroup[]>([]);
  protected readonly selected = signal<string[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly dirty = computed(() => {
    const original = this.role()?.permissionIds ?? [];
    const current = this.selected();
    return original.length !== current.length || current.some((id) => !original.includes(id));
  });

  ngOnInit(): void {
    forkJoin([this.api.getRole(this.id()), this.api.getPermissions()]).subscribe({
      next: ([role, groups]) => {
        this.role.set(role);
        this.groups.set(groups);
        this.selected.set([...role.permissionIds]);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(error, 'The role could not be loaded.'));
      },
    });
  }

  protected save(): void {
    this.saving.set(true);
    this.error.set(null);
    this.api.setRolePermissions(this.id(), this.selected()).subscribe({
      next: (role) => {
        this.role.set(role);
        this.selected.set([...role.permissionIds]);
        this.saving.set(false);
        this.notifications.success('Role permissions saved.');
      },
      error: (error) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(error));
      },
    });
  }
}
