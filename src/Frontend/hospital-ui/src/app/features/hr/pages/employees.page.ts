import { DatePipe } from '@angular/common';
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
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../../administration/administration-api.service';
import { DepartmentLookupItem } from '../../administration/administration.models';
import { HrApiService } from '../hr-api.service';
import {
  EMPLOYMENT_STATUSES,
  EMPLOYMENT_TYPES,
  EmployeeListItem,
  EmployeeQuery,
  EmploymentStatus,
  EmploymentType,
  HrPermissions,
} from '../hr.models';
import { EmployeeFormDialog } from './employee-form.dialog';
import { EmployeeStatusDialog } from './employee-status.dialog';

@Component({
  selector: 'app-employees-page',
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
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './employees.page.html',
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class EmployeesPage {
  private readonly api = inject(HrApiService);
  private readonly dialog = inject(MatDialog);

  protected readonly permissions = HrPermissions;
  protected readonly statuses = EMPLOYMENT_STATUSES;
  protected readonly types = EMPLOYMENT_TYPES;
  protected readonly columns = ['employeeCode', 'fullName', 'department', 'employmentType', 'joiningDate', 'status', 'actions'];
  protected readonly departments = signal<DepartmentLookupItem[]>([]);

  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly department = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<EmploymentStatus | ''>('', { nonNullable: true });
  protected readonly type = new FormControl<EmploymentType | ''>('', { nonNullable: true });

  protected readonly query = signal<EmployeeQuery>({ pageNumber: 1, pageSize: 20, sortBy: 'fullName', sortDirection: 'Asc' });
  protected readonly page = signal<PagedResponse<EmployeeListItem> | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    inject(AdministrationApiService).departmentLookup().subscribe({ next: (d) => this.departments.set(d), error: () => undefined });

    toObservable(this.query)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.loadError.set(null);
        }),
        switchMap((q) =>
          this.api.search(q).pipe(
            catchError((error: unknown) => {
              this.loadError.set(apiErrorMessage(error, 'Employees could not be loaded.'));
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
    this.department.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((departmentId) => this.query.update((q) => ({ ...q, departmentId: departmentId || undefined, pageNumber: 1 })));
    this.status.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((status) => this.query.update((q) => ({ ...q, status: status || undefined, pageNumber: 1 })));
    this.type.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((type) => this.query.update((q) => ({ ...q, employmentType: type || undefined, pageNumber: 1 })));
  }

  protected statusLabel(status: EmploymentStatus): string {
    return EMPLOYMENT_STATUSES.find((s) => s.value === status)?.label ?? status;
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
      sortBy: sort.direction ? sort.active : 'fullName',
      sortDirection: sort.direction === 'desc' ? 'Desc' : 'Asc',
    }));
  }

  protected create(): void {
    this.dialog
      .open(EmployeeFormDialog, { data: null, width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }

  protected edit(employee: EmployeeListItem): void {
    this.api
      .get(employee.id)
      .pipe(switchMap((detail) => this.dialog.open(EmployeeFormDialog, { data: detail, width: '640px', maxWidth: '95vw' }).afterClosed()))
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }

  protected changeStatus(employee: EmployeeListItem): void {
    this.dialog
      .open(EmployeeStatusDialog, { data: employee, width: '420px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.reload());
  }
}
