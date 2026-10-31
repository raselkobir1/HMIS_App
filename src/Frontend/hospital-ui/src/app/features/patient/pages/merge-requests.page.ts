import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { catchError, filter, of, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../../core/models/api-response.model';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialog } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { PatientApiService } from '../patient-api.service';
import { MergeRequest, MergeStatus } from '../patient.models';

@Component({
  selector: 'app-merge-requests-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Duplicate Merge Requests" subtitle="Review reported duplicate patient records" />
    <section class="filters">
      <mat-form-field>
        <mat-label>Status</mat-label>
        <mat-select [formControl]="status">
          <mat-option value="Pending">Pending</mat-option>
          <mat-option value="Approved">Approved</mat-option>
          <mat-option value="Rejected">Rejected</mat-option>
          <mat-option value="">All</mat-option>
        </mat-select>
      </mat-form-field>
    </section>
    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="table-scroll">
        <table mat-table [dataSource]="page()?.items ?? []">
          <ng-container matColumnDef="duplicate">
            <th mat-header-cell *matHeaderCellDef>Duplicate (will be merged)</th>
            <td mat-cell *matCellDef="let r">
              <a [routerLink]="['/patients', r.sourcePatientId]">{{ r.sourceMRNumber }}</a>
              <div class="secondary">{{ r.sourceName }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="survivor">
            <th mat-header-cell *matHeaderCellDef>Surviving record</th>
            <td mat-cell *matCellDef="let r">
              <a [routerLink]="['/patients', r.targetPatientId]">{{ r.targetMRNumber }}</a>
              <div class="secondary">{{ r.targetName }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="reason">
            <th mat-header-cell *matHeaderCellDef>Reason</th>
            <td mat-cell *matCellDef="let r">
              {{ r.reason }}
              <div class="secondary">{{ r.requestedAt | date: 'dd MMM yyyy, h:mm a' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let r">
              <span class="badge" [class.info]="r.status === 'Pending'" [class.ok]="r.status === 'Approved'" [class.muted]="r.status === 'Rejected'">{{ r.status }}</span>
              @if (r.reviewNote) {
                <div class="secondary">{{ r.reviewNote }}</div>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let r">
              @if (r.status === 'Pending') {
                <button mat-flat-button type="button" (click)="review(r, true)">Approve</button>
                <button mat-button type="button" (click)="review(r, false)">Reject</button>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No merge requests.' }}</td>
          </tr>
        </table>
      </div>
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="pageIndex()" [pageSize]="20" (page)="onPage($event)" aria-label="Merge request pages" />
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class MergeRequestsPage {
  private readonly api = inject(PatientApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly columns = ['duplicate', 'survivor', 'reason', 'status', 'actions'];
  protected readonly status = new FormControl<MergeStatus | ''>('Pending', { nonNullable: true });
  protected readonly filterState = signal<{ status: MergeStatus | ''; page: number }>({ status: 'Pending', page: 1 });
  protected readonly pageIndex = signal(0);
  protected readonly page = signal<PagedResponse<MergeRequest> | null>(null);
  protected readonly loading = signal(false);

  constructor() {
    toObservable(this.filterState)
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap((f) =>
          this.api.mergeRequests(f.status || undefined, f.page, 20).pipe(
            catchError((err: unknown) => {
              this.notifications.error(apiErrorMessage(err, 'Merge requests could not be loaded.'));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.page.set(page);
        this.loading.set(false);
      });

    this.status.valueChanges.pipe(takeUntilDestroyed()).subscribe((status) => {
      this.pageIndex.set(0);
      this.filterState.set({ status, page: 1 });
    });
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.filterState.update((f) => ({ ...f, page: event.pageIndex + 1 }));
  }

  protected review(request: MergeRequest, approve: boolean): void {
    this.dialog
      .open(ConfirmDialog, {
        data: approve
          ? {
              title: 'Approve merge',
              message: `${request.sourceMRNumber} (${request.sourceName}) will become read-only and point to ${request.targetMRNumber}. This cannot be undone.`,
              confirmText: 'Approve merge',
              destructive: true,
            }
          : { title: 'Reject merge', message: 'Both records stay as they are.', confirmText: 'Reject' },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.reviewMerge(request.id, approve, null)),
      )
      .subscribe({
        next: () => {
          this.notifications.success(approve ? 'Records merged.' : 'Merge request rejected.');
          this.filterState.update((f) => ({ ...f }));
        },
        error: (err) => this.notifications.error(apiErrorMessage(err)),
      });
  }
}
