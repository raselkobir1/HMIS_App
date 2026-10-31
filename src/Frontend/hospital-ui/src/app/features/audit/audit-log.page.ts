import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, of, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../core/models/api-response.model';
import { NotificationService } from '../../core/services/notification.service';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../shared/utilities/server-errors';
import { AuditApiService } from './audit-api.service';
import { AuditDetailDialog } from './audit-detail.dialog';
import { AuditFilterOptions, AuditLogListItem, AuditLogQuery } from './audit.models';

@Component({
  selector: 'app-audit-log-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './audit-log.page.html',
  styleUrl: './audit-log.page.scss',
})
export class AuditLogPage {
  private readonly api = inject(AuditApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly columns = ['timestamp', 'user', 'action', 'entity', 'module'];
  protected readonly options = signal<AuditFilterOptions>({ modules: [], entityNames: [], actions: [] });
  protected readonly query = signal<AuditLogQuery>({ pageNumber: 1, pageSize: 20 });
  protected readonly page = signal<PagedResponse<AuditLogListItem> | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly filters = new FormGroup({
    from: new FormControl<Date | null>(null),
    to: new FormControl<Date | null>(null),
    module: new FormControl<string>('', { nonNullable: true }),
    entityName: new FormControl<string>('', { nonNullable: true }),
    action: new FormControl<string>('', { nonNullable: true }),
    entityId: new FormControl<string>('', { nonNullable: true }),
    search: new FormControl<string>('', { nonNullable: true }),
  });

  constructor() {
    this.api.filterOptions().subscribe({ next: (options) => this.options.set(options), error: () => undefined });

    toObservable(this.query)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.loadError.set(null);
        }),
        switchMap((query) =>
          this.api.search(query).pipe(
            catchError((error: unknown) => {
              this.loadError.set(apiErrorMessage(error, 'The audit log could not be loaded.'));
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

    this.filters.valueChanges.pipe(debounceTime(350), takeUntilDestroyed()).subscribe(() => this.applyFilters());
  }

  protected clearFilters(): void {
    this.filters.reset();
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected open(row: AuditLogListItem): void {
    this.api.get(row.id).subscribe({
      next: (detail) => this.dialog.open(AuditDetailDialog, { data: detail, width: '720px', maxWidth: '95vw' }),
      error: (error) => this.notifications.error(apiErrorMessage(error)),
    });
  }

  private applyFilters(): void {
    const f = this.filters.getRawValue();
    const to = f.to ? new Date(f.to.getFullYear(), f.to.getMonth(), f.to.getDate(), 23, 59, 59, 999) : null;

    this.query.update((q) => ({
      pageNumber: 1,
      pageSize: q.pageSize,
      // Dates are picked in local time (Asia/Dhaka) and sent as UTC instants.
      fromUtc: f.from?.toISOString(),
      toUtc: to?.toISOString(),
      module: f.module || undefined,
      entityName: f.entityName || undefined,
      action: f.action || undefined,
      entityId: f.entityId.trim() || undefined,
      search: f.search.trim() || undefined,
    }));
  }
}
