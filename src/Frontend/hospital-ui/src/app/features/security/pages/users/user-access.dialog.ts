import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { forkJoin, switchMap } from 'rxjs';

import { NotificationService } from '../../../../core/services/notification.service';
import { apiErrorMessage } from '../../../../shared/utilities/server-errors';
import { PermissionChecklist } from '../../components/permission-checklist';
import { PermissionGroup, RoleListItem, UserDetail } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';

/** Assigns roles and direct (per-user) permissions. */
@Component({
  selector: 'app-user-access-dialog',
  imports: [MatDialogModule, MatTabsModule, MatCheckboxModule, MatButtonModule, MatProgressBarModule, PermissionChecklist],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Access for {{ user.fullName }}</h2>
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <mat-dialog-content>
      @if (error(); as message) {
        <div class="banner" role="alert">{{ message }}</div>
      }
      <mat-tab-group>
        <mat-tab label="Roles">
          <div class="role-list">
            @for (role of roles(); track role.id) {
              <mat-checkbox [checked]="roleIds().includes(role.id)" (change)="toggleRole(role.id, $event.checked)">
                <strong>{{ role.name }}</strong>
                @if (role.description) {
                  <span class="hint"> — {{ role.description }}</span>
                }
              </mat-checkbox>
            }
          </div>
        </mat-tab>
        <mat-tab label="Direct permissions">
          <p class="hint">Granted to this user in addition to their roles. Prefer roles for normal access.</p>
          <app-permission-checklist [groups]="groups()" [(selected)]="permissionIds" />
        </mat-tab>
      </mat-tab-group>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button type="button" [disabled]="saving() || loading()" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
  styleUrl: '../../../../shared/styles/dialog.scss',
})
export class UserAccessDialog {
  protected readonly user = inject<UserDetail>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<UserAccessDialog, boolean>);
  private readonly api = inject(SecurityApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly roles = signal<RoleListItem[]>([]);
  protected readonly groups = signal<PermissionGroup[]>([]);
  protected readonly roleIds = signal<string[]>(this.user.roles.map((r) => r.id));
  protected readonly permissionIds = signal<string[]>([...this.user.directPermissionIds]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    forkJoin([this.api.searchRoles(), this.api.getPermissions()]).subscribe({
      next: ([roles, groups]) => {
        this.roles.set(roles.items);
        this.groups.set(groups);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(error, 'Roles and permissions could not be loaded.'));
      },
    });
  }

  protected toggleRole(id: string, checked: boolean): void {
    this.roleIds.update((ids) => (checked ? [...ids, id] : ids.filter((x) => x !== id)));
  }

  protected save(): void {
    this.saving.set(true);
    this.error.set(null);
    this.api
      .setUserRoles(this.user.id, this.roleIds())
      .pipe(switchMap(() => this.api.setUserPermissions(this.user.id, this.permissionIds())))
      .subscribe({
        next: () => {
          this.notifications.success('Access updated.');
          this.dialogRef.close(true);
        },
        error: (error) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(error));
        },
      });
  }
}
