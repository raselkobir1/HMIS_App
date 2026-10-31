import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../../../core/models/api-response.model';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../../shared/utilities/server-errors';
import { LoginHistoryEntry, LoginHistoryQuery } from '../../models/security.models';
import { SecurityApiService } from '../../services/security-api.service';

const FAILURE_LABELS: Record<string, string> = {
  UnknownUser: 'Unknown user',
  InvalidPassword: 'Wrong password',
  LockedOut: 'Account locked',
  Inactive: 'Account inactive',
};

@Component({
  selector: 'app-login-history-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressBarModule,
    PageHeader,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Login History" subtitle="Every sign-in attempt, most recent first" />

    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Username or email</mat-label>
        <input matInput [formControl]="search" />
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Result</mat-label>
        <mat-select [formControl]="result">
          <mat-option value="all">All</mat-option>
          <mat-option value="success">Succeeded</mat-option>
          <mat-option value="failed">Failed</mat-option>
        </mat-select>
      </mat-form-field>
    </section>

    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (loadError(); as error) {
        <div class="state error" role="alert"><mat-icon>error</mat-icon><span>{{ error }}</span></div>
      } @else {
        <div class="table-scroll">
          <table mat-table [dataSource]="page()?.items ?? []">
            <ng-container matColumnDef="attemptedAt">
              <th mat-header-cell *matHeaderCellDef>Time</th>
              <td mat-cell *matCellDef="let e">{{ e.attemptedAt | date: 'dd MMM yyyy, h:mm:ss a' }}</td>
            </ng-container>
            <ng-container matColumnDef="user">
              <th mat-header-cell *matHeaderCellDef>Username entered</th>
              <td mat-cell *matCellDef="let e">{{ e.userNameAttempted }}</td>
            </ng-container>
            <ng-container matColumnDef="result">
              <th mat-header-cell *matHeaderCellDef>Result</th>
              <td mat-cell *matCellDef="let e">
                @if (e.succeeded) {
                  <span class="badge ok">Success</span>
                } @else {
                  <span class="badge warn">{{ failureLabel(e) }}</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="ip">
              <th mat-header-cell *matHeaderCellDef>IP address</th>
              <td mat-cell *matCellDef="let e">
                <span [matTooltip]="e.userAgent ?? ''">{{ e.ipAddress ?? '—' }}</span>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
            <tr class="mat-row" *matNoDataRow>
              <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No sign-in attempts found.' }}</td>
            </tr>
          </table>
        </div>
        <mat-paginator
          [length]="page()?.totalCount ?? 0"
          [pageIndex]="query().pageNumber - 1"
          [pageSize]="query().pageSize"
          [pageSizeOptions]="[20, 50, 100]"
          (page)="onPage($event)"
          aria-label="Login history pages"
        />
      }
    </div>
  `,
  styleUrl: '../../../../shared/styles/list-page.scss',
})
export class LoginHistoryPage {
  private readonly api = inject(SecurityApiService);

  protected readonly columns = ['attemptedAt', 'user', 'result', 'ip'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly result = new FormControl<'all' | 'success' | 'failed'>('all', { nonNullable: true });
  protected readonly query = signal<LoginHistoryQuery>({ pageNumber: 1, pageSize: 20 });
  protected readonly page = signal<PagedResponse<LoginHistoryEntry> | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    toObservable(this.query)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.loadError.set(null);
        }),
        switchMap((query) =>
          this.api.searchLoginHistory(query).pipe(
            catchError((error: unknown) => {
              this.loadError.set(apiErrorMessage(error, 'Login history could not be loaded.'));
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

    this.result.valueChanges.pipe(takeUntilDestroyed()).subscribe((result) =>
      this.query.update((q) => ({ ...q, succeeded: result === 'all' ? undefined : result === 'success', pageNumber: 1 })),
    );
  }

  protected failureLabel(entry: LoginHistoryEntry): string {
    return FAILURE_LABELS[entry.failureReason ?? ''] ?? 'Failed';
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }
}
