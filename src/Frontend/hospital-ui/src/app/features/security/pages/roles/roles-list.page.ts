import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';

import { NotificationService } from '../../../../core/services/notification.service';
import { ConfirmDialog, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../shared/utilities/server-errors';
import { RoleDetail, RoleListItem, SecurityPermissions } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';
import { RoleFormDialog } from './role-form.dialog';

@Component({
  selector: 'app-roles-list-page',
  imports: [
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Roles & Permissions" subtitle="What each role is allowed to do">
      <button mat-flat-button type="button" *hasPermission="permissions.rolesManage" (click)="create()">
        <mat-icon>add</mat-icon>
        New role
      </button>
    </app-page-header>

    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (loadError(); as error) {
        <div class="state error" role="alert">
          <mat-icon>error</mat-icon><span>{{ error }}</span>
          <button mat-button type="button" (click)="load()">Retry</button>
        </div>
      } @else {
        <div class="table-scroll">
          <table mat-table [dataSource]="roles()">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Role</th>
              <td mat-cell *matCellDef="let role">
                <div>
                  {{ role.name }}
                  @if (role.isSystem) {
                    <span class="badge muted">System</span>
                  }
                </div>
                @if (role.description) {
                  <div class="secondary">{{ role.description }}</div>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="userCount">
              <th mat-header-cell *matHeaderCellDef>Users</th>
              <td mat-cell *matCellDef="let role">{{ role.userCount }}</td>
            </ng-container>
            <ng-container matColumnDef="permissionCount">
              <th mat-header-cell *matHeaderCellDef>Permissions</th>
              <td mat-cell *matCellDef="let role">{{ role.permissionCount }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef class="actions-col"></th>
              <td mat-cell *matCellDef="let role" class="actions-col">
                <button mat-icon-button type="button" [matMenuTriggerFor]="menu" [attr.aria-label]="'Actions for ' + role.name">
                  <mat-icon>more_vert</mat-icon>
                </button>
                <mat-menu #menu="matMenu" xPosition="before">
                  <a mat-menu-item [routerLink]="[role.id, 'permissions']">
                    <mat-icon>checklist</mat-icon><span>Permissions</span>
                  </a>
                  <button mat-menu-item type="button" *hasPermission="permissions.rolesManage" (click)="edit(role)">
                    <mat-icon>edit</mat-icon><span>Edit</span>
                  </button>
                  @if (!role.isSystem) {
                    <button mat-menu-item type="button" *hasPermission="permissions.rolesManage" (click)="remove(role)">
                      <mat-icon>delete</mat-icon><span>Delete</span>
                    </button>
                  }
                </mat-menu>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
            <tr class="mat-row" *matNoDataRow>
              <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No roles found.' }}</td>
            </tr>
          </table>
        </div>
      }
    </div>
  `,
  styleUrl: '../security-pages.scss',
})
export class RolesListPage {
  private readonly api = inject(SecurityApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly permissions = SecurityPermissions;
  protected readonly columns = ['name', 'userCount', 'permissionCount', 'actions'];
  protected readonly roles = signal<RoleListItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api.searchRoles().subscribe({
      next: (page) => {
        this.roles.set(page.items);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.loadError.set(apiErrorMessage(error, 'Roles could not be loaded.'));
      },
    });
  }

  protected create(): void {
    this.openForm(null);
  }

  protected edit(role: RoleListItem): void {
    this.api.getRole(role.id).subscribe((detail) => this.openForm(detail));
  }

  protected remove(role: RoleListItem): void {
    const data: ConfirmDialogData = {
      title: 'Delete role',
      message: `Delete the role "${role.name}"? This cannot be undone.`,
      confirmText: 'Delete',
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.deleteRole(role.id)),
      )
      .subscribe({
        next: () => {
          this.notifications.success('Role deleted.');
          this.load();
        },
        error: (error) => this.notifications.error(apiErrorMessage(error)),
      });
  }

  private openForm(role: RoleDetail | null): void {
    this.dialog
      .open(RoleFormDialog, { data: role, width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.load());
  }
}
