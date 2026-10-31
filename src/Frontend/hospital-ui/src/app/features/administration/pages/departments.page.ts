import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
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
import { catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../../core/models/api-response.model';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../administration-api.service';
import { AdminPermissions, Department, DEPARTMENT_TYPES, DepartmentQuery, DepartmentType } from '../administration.models';
import { DepartmentFormDialog } from './department-form.dialog';

@Component({
  selector: 'app-departments-page',
  imports: [
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
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Departments" subtitle="Clinical, diagnostic, administrative and support units">
      <button mat-flat-button type="button" *hasPermission="permissions.departmentsManage" (click)="openForm(null)">
        <mat-icon>add</mat-icon>
        New department
      </button>
    </app-page-header>

    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Search</mat-label>
        <input matInput [formControl]="search" placeholder="Code or name" />
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Type</mat-label>
        <mat-select [formControl]="type">
          <mat-option value="">All</mat-option>
          @for (t of types; track t) {
            <mat-option [value]="t">{{ t }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Status</mat-label>
        <mat-select [formControl]="status">
          <mat-option value="all">All</mat-option>
          <mat-option value="active">Active</mat-option>
          <mat-option value="inactive">Inactive</mat-option>
        </mat-select>
      </mat-form-field>
    </section>

    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (loadError(); as error) {
        <div class="state error" role="alert">
          <mat-icon>error</mat-icon><span>{{ error }}</span>
          <button mat-button type="button" (click)="reload()">Retry</button>
        </div>
      } @else {
        <div class="table-scroll">
          <table mat-table [dataSource]="page()?.items ?? []" matSort (matSortChange)="onSort($event)">
            <ng-container matColumnDef="code">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Code</th>
              <td mat-cell *matCellDef="let d">{{ d.code }}</td>
            </ng-container>
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
              <td mat-cell *matCellDef="let d">
                <div>{{ d.name }}</div>
                @if (d.parentName) {
                  <div class="secondary">Sub-department of {{ d.parentName }}</div>
                } @else if (d.subDepartmentCount) {
                  <div class="secondary">{{ d.subDepartmentCount }} sub-department(s)</div>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Type</th>
              <td mat-cell *matCellDef="let d">{{ d.type }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let d">
                <span class="badge" [class.ok]="d.isActive" [class.muted]="!d.isActive">{{ d.isActive ? 'Active' : 'Inactive' }}</span>
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef class="actions-col"></th>
              <td mat-cell *matCellDef="let d" class="actions-col">
                <ng-container *hasPermission="permissions.departmentsManage">
                  <button mat-icon-button type="button" [matMenuTriggerFor]="menu" [attr.aria-label]="'Actions for ' + d.name">
                    <mat-icon>more_vert</mat-icon>
                  </button>
                  <mat-menu #menu="matMenu" xPosition="before">
                    <button mat-menu-item type="button" (click)="openForm(d)"><mat-icon>edit</mat-icon><span>Edit</span></button>
                    <button mat-menu-item type="button" (click)="toggle(d)">
                      <mat-icon>{{ d.isActive ? 'block' : 'check_circle' }}</mat-icon>
                      <span>{{ d.isActive ? 'Deactivate' : 'Activate' }}</span>
                    </button>
                  </mat-menu>
                </ng-container>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
            <tr class="mat-row" *matNoDataRow>
              <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No departments match the current filters.' }}</td>
            </tr>
          </table>
        </div>
        <mat-paginator
          [length]="page()?.totalCount ?? 0"
          [pageIndex]="query().pageNumber - 1"
          [pageSize]="query().pageSize"
          [pageSizeOptions]="[20, 50, 100]"
          (page)="onPage($event)"
          aria-label="Department pages"
        />
      }
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class DepartmentsPage {
  private readonly api = inject(AdministrationApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly permissions = AdminPermissions;
  protected readonly types = DEPARTMENT_TYPES;
  protected readonly columns = ['code', 'name', 'type', 'status', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly type = new FormControl<DepartmentType | ''>('', { nonNullable: true });
  protected readonly status = new FormControl<'all' | 'active' | 'inactive'>('all', { nonNullable: true });
  protected readonly query = signal<DepartmentQuery>({ pageNumber: 1, pageSize: 50, sortBy: 'name', sortDirection: 'Asc' });
  protected readonly page = signal<PagedResponse<Department> | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    toObservable(this.query)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.loadError.set(null);
        }),
        switchMap((q) =>
          this.api.searchDepartments(q).pipe(
            catchError((error: unknown) => {
              this.loadError.set(apiErrorMessage(error, 'Departments could not be loaded.'));
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
    this.type.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((type) => this.query.update((q) => ({ ...q, type: type || undefined, pageNumber: 1 })));
    this.status.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((s) => this.query.update((q) => ({ ...q, isActive: s === 'all' ? undefined : s === 'active', pageNumber: 1 })));
  }

  protected reload(): void {
    this.query.update((q) => ({ ...q }));
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected onSort(sort: Sort): void {
    this.query.update((q) => ({
      ...q,
      sortBy: sort.direction ? sort.active : 'name',
      sortDirection: sort.direction === 'desc' ? 'Desc' : 'Asc',
    }));
  }

  protected openForm(department: Department | null): void {
    this.dialog
      .open(DepartmentFormDialog, { data: department, width: '520px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }

  protected toggle(department: Department): void {
    const activate = !department.isActive;
    this.dialog
      .open(ConfirmDialog, {
        data: {
          title: activate ? 'Activate department' : 'Deactivate department',
          message: activate
            ? `Make "${department.name}" available again?`
            : `"${department.name}" will no longer be offered for new records. Existing records are not changed.`,
          confirmText: activate ? 'Activate' : 'Deactivate',
          destructive: !activate,
        },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.setDepartmentStatus(department.id, activate)),
      )
      .subscribe({
        next: () => {
          this.notifications.success(activate ? 'Department activated.' : 'Department deactivated.');
          this.reload();
        },
        error: (error) => this.notifications.error(apiErrorMessage(error)),
      });
  }
}
