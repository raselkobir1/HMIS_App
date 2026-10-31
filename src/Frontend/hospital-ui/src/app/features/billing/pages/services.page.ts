import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, filter, of, Subject, switchMap, tap } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { PagedResponse } from '../../../core/models/api-response.model';
import { NotificationService } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { BillableService, BillingApiService, BillingPermissions, SERVICE_CATEGORIES, ServiceQuery } from '../billing.models';
import { ServiceDialog } from '../components/service-dialog';
import { TakaPipe } from '../components/taka.pipe';

/** Service price list. Route: /billing/services. */
@Component({
  selector: 'app-services-page',
  imports: [
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
    TakaPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Price list" subtitle="Services that can be billed; invoices keep the price at the time of billing">
      @if (canManage) {
        <button mat-flat-button type="button" (click)="edit(null)"><mat-icon>add</mat-icon> Add service</button>
      }
    </app-page-header>
    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Search code or name</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input matInput [ngModel]="query().search ?? ''" (ngModelChange)="search$.next($event)" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Category</mat-label>
        <mat-select [ngModel]="query().category ?? ''" (ngModelChange)="patch({ category: $event || undefined })">
          <mat-option value="">All categories</mat-option>
          @for (c of categories; track c) {
            <mat-option [value]="c">{{ c }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Status</mat-label>
        <mat-select [ngModel]="query().isActive ?? ''" (ngModelChange)="patch({ isActive: $event === '' ? undefined : $event })">
          <mat-option value="">All</mat-option>
          <mat-option [value]="true">Active</mat-option>
          <mat-option [value]="false">Inactive</mat-option>
        </mat-select>
      </mat-form-field>
    </section>
    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="table-scroll">
        <table mat-table [dataSource]="page()?.items ?? []">
          <ng-container matColumnDef="code">
            <th mat-header-cell *matHeaderCellDef>Code</th>
            <td mat-cell *matCellDef="let s">{{ s.code }}</td>
          </ng-container>
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Service</th>
            <td mat-cell *matCellDef="let s">
              {{ s.name }}
              <div class="secondary">{{ s.category }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="price">
            <th mat-header-cell *matHeaderCellDef class="num">Price</th>
            <td mat-cell *matCellDef="let s" class="num">
              {{ s.price | taka }}
              @if (s.isPriceEditable) {
                <div class="secondary">Price set at billing</div>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let s">
              <span class="badge" [class.ok]="s.isActive" [class.muted]="!s.isActive">{{ s.isActive ? 'Active' : 'Inactive' }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="actions-col"></th>
            <td mat-cell *matCellDef="let s" class="actions-col">
              @if (canManage) {
                <div class="row-actions">
                  <button mat-icon-button type="button" (click)="edit(s)" [attr.aria-label]="'Edit ' + s.name"><mat-icon>edit</mat-icon></button>
                  <button mat-icon-button type="button" (click)="toggle(s)" [attr.aria-label]="(s.isActive ? 'Deactivate ' : 'Activate ') + s.name">
                    <mat-icon>{{ s.isActive ? 'toggle_on' : 'toggle_off' }}</mat-icon>
                  </button>
                </div>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No services yet.' }}</td>
          </tr>
        </table>
      </div>
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="query().pageNumber - 1" [pageSize]="query().pageSize"
                     [pageSizeOptions]="[25, 50, 100]" (page)="onPage($event)" aria-label="Service pages" />
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    .num { text-align: right; }
    .actions-col { width: 112px; }
    .row-actions { display: flex; justify-content: flex-end; }
  `,
})
export class ServicesPage {
  private readonly api = inject(BillingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly canManage = inject(AuthService).hasPermission(BillingPermissions.servicesManage);
  protected readonly categories = SERVICE_CATEGORIES;
  protected readonly columns = ['code', 'name', 'price', 'status', 'actions'];
  protected readonly query = signal<ServiceQuery>({ pageNumber: 1, pageSize: 50 });
  protected readonly page = signal<PagedResponse<BillableService> | null>(null);
  protected readonly loading = signal(false);
  protected readonly search$ = new Subject<string>();

  constructor() {
    this.search$.pipe(debounceTime(300), takeUntilDestroyed()).subscribe((search) => this.patch({ search: search.trim() || undefined }));
    toObservable(this.query)
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap((q) => this.api.services(q).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.page.set(page);
        this.loading.set(false);
      });
  }

  protected patch(change: Partial<ServiceQuery>): void {
    this.query.update((q) => ({ ...q, ...change, pageNumber: 1 }));
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected edit(service: BillableService | null): void {
    this.dialog
      .open(ServiceDialog, { data: service, width: '560px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.query.update((q) => ({ ...q })));
  }

  protected toggle(service: BillableService): void {
    this.api.setServiceStatus(service.id, !service.isActive).subscribe({
      next: (s) => {
        this.notifications.success(s.isActive ? `${s.name} activated.` : `${s.name} deactivated; it can no longer be billed.`);
        this.query.update((q) => ({ ...q }));
      },
      error: (err) => this.notifications.error(apiErrorMessage(err)),
    });
  }
}
