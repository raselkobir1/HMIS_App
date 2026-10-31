import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { PagedResponse } from '../../../../core/models/api-response.model';
import { NotificationService } from '../../../../core/services/notification.service';
import { ConfirmDialog, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../shared/utilities/server-errors';
import { SecurityPermissions, UserListItem, UserListQuery } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';
import { ResetPasswordDialog } from './reset-password.dialog';
import { UserAccessDialog } from './user-access.dialog';
import { UserFormDialog, UserFormDialogData } from './user-form.dialog';

@Component({
  selector: 'app-users-list-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-list.page.html',
  styleUrl: '../security-pages.scss',
})
export class UsersListPage {
  private readonly api = inject(SecurityApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  protected readonly permissions = SecurityPermissions;
  protected readonly columns = ['userName', 'fullName', 'roles', 'status', 'lastLoginAt', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<'all' | 'active' | 'inactive'>('all', { nonNullable: true });

  protected readonly query = signal<UserListQuery>({ pageNumber: 1, pageSize: 20, sortBy: 'userName', sortDirection: 'Asc' });
  protected readonly page = signal<PagedResponse<UserListItem> | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly currentUserId = this.auth.user()?.id;

  constructor() {
    toObservable(this.query)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.loadError.set(null);
        }),
        switchMap((query) =>
          this.api.searchUsers(query).pipe(
            catchError((error: unknown) => {
              this.loadError.set(apiErrorMessage(error, 'Users could not be loaded.'));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        if (page) {
          this.page.set(page);
        }
        this.loading.set(false);
      });

    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((search) => this.query.update((q) => ({ ...q, search: search.trim() || undefined, pageNumber: 1 })));

    this.status.valueChanges.pipe(takeUntilDestroyed()).subscribe((status) =>
      this.query.update((q) => ({ ...q, isActive: status === 'all' ? undefined : status === 'active', pageNumber: 1 })),
    );
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected onSort(sort: Sort): void {
    this.query.update((q) => ({
      ...q,
      sortBy: sort.direction ? sort.active : 'userName',
      sortDirection: sort.direction === 'desc' ? 'Desc' : 'Asc',
    }));
  }

  protected reload(): void {
    this.query.update((q) => ({ ...q }));
  }

  protected create(): void {
    this.openForm({ mode: 'create' });
  }

  protected edit(user: UserListItem): void {
    this.api.getUser(user.id).subscribe((detail) => this.openForm({ mode: 'edit', user: detail }));
  }

  protected manageAccess(user: UserListItem): void {
    this.api.getUser(user.id).subscribe((detail) =>
      this.dialog
        .open(UserAccessDialog, { data: detail, width: '720px', maxWidth: '95vw' })
        .afterClosed()
        .pipe(filter(Boolean))
        .subscribe(() => this.reload()),
    );
  }

  protected resetPassword(user: UserListItem): void {
    this.dialog.open(ResetPasswordDialog, { data: user, width: '440px', maxWidth: '95vw' });
  }

  protected toggleStatus(user: UserListItem): void {
    const activate = !user.isActive;
    const data: ConfirmDialogData = {
      title: activate ? 'Activate user' : 'Deactivate user',
      message: activate
        ? `Allow ${user.fullName} to sign in again?`
        : `${user.fullName} will be signed out immediately and will not be able to sign in.`,
      confirmText: activate ? 'Activate' : 'Deactivate',
      destructive: !activate,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.setUserStatus(user.id, activate)),
      )
      .subscribe({
        next: () => {
          this.notifications.success(activate ? 'User activated.' : 'User deactivated.');
          this.reload();
        },
        error: (error) => this.notifications.error(apiErrorMessage(error)),
      });
  }

  protected unlock(user: UserListItem): void {
    this.api.unlockUser(user.id).subscribe({
      next: () => {
        this.notifications.success('User unlocked.');
        this.reload();
      },
      error: (error) => this.notifications.error(apiErrorMessage(error)),
    });
  }

  private openForm(data: UserFormDialogData): void {
    this.dialog
      .open(UserFormDialog, { data, width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }
}
