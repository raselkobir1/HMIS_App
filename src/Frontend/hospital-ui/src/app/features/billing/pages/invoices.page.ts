import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, of, Subject, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../../core/models/api-response.model';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { toIsoDate } from '../../../shared/utilities/dates';
import { BillingApiService, BillingPermissions, invoiceBadge, InvoiceQuery, InvoiceStatus, InvoiceSummary, SOURCE_LABELS } from '../billing.models';
import { TakaPipe } from '../components/taka.pipe';

/** Invoice list for the cash counter. Route: /billing/invoices. */
@Component({
  selector: 'app-invoices-page',
  imports: [
    FormsModule,
    RouterLink,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
    TakaPipe,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Invoices" subtitle="OPD bills are created automatically at check-in">
      <a mat-flat-button routerLink="/billing/invoices/new" *hasPermission="permissions.invoiceManage"><mat-icon>add</mat-icon> New invoice</a>
    </app-page-header>
    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Invoice or appointment number</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input matInput [ngModel]="query().search ?? ''" (ngModelChange)="search$.next($event)" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Dates</mat-label>
        <mat-date-range-input [rangePicker]="range">
          <input matStartDate placeholder="From" [ngModel]="from()" (ngModelChange)="onFrom($event)" />
          <input matEndDate placeholder="To" [ngModel]="to()" (ngModelChange)="onTo($event)" />
        </mat-date-range-input>
        <mat-datepicker-toggle matIconSuffix [for]="range" />
        <mat-date-range-picker #range />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Status</mat-label>
        <mat-select [ngModel]="query().status ?? ''" (ngModelChange)="patch({ status: $event || undefined })">
          <mat-option value="">All statuses</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ s }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-checkbox [ngModel]="query().dueOnly ?? false" (ngModelChange)="patch({ dueOnly: $event || undefined })">Amount due only</mat-checkbox>
    </section>
    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="table-scroll">
        <table mat-table [dataSource]="page()?.items ?? []">
          <ng-container matColumnDef="number">
            <th mat-header-cell *matHeaderCellDef>Invoice</th>
            <td mat-cell *matCellDef="let i">
              <a [routerLink]="['/billing/invoices', i.id]">{{ i.invoiceNumber }}</a>
              <div class="secondary">{{ i.invoiceDate }}{{ i.sourceType ? ' · ' + (sources[i.sourceType] ?? i.sourceType) : '' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="patient">
            <th mat-header-cell *matHeaderCellDef>Patient</th>
            <td mat-cell *matCellDef="let i">
              <a [routerLink]="['/billing/patients', i.patientId]">{{ i.patientName ?? 'Unknown' }}</a>
              <div class="secondary">{{ i.patientMRNumber }}{{ i.sourceReference ? ' · ' + i.sourceReference : '' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="net">
            <th mat-header-cell *matHeaderCellDef class="num">Net</th>
            <td mat-cell *matCellDef="let i" class="num">
              {{ i.netAmount | taka }}
              @if (i.discountAmount > 0) {
                <div class="secondary">after {{ i.discountAmount | taka }} discount</div>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="due">
            <th mat-header-cell *matHeaderCellDef class="num">Due</th>
            <td mat-cell *matCellDef="let i" class="num" [class.due]="i.dueAmount > 0">{{ i.dueAmount | taka }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let i">
              @let badge = badgeOf(i);
              <span [class]="badge.css">{{ badge.label }}</span>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No invoices for these filters.' }}</td>
          </tr>
        </table>
      </div>
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="query().pageNumber - 1" [pageSize]="query().pageSize"
                     [pageSizeOptions]="[25, 50, 100]" (page)="onPage($event)" aria-label="Invoice pages" />
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    .num { text-align: right; }
    .due { font-weight: 600; color: var(--mat-sys-error); }
    mat-checkbox { align-self: center; }
  `,
})
export class InvoicesPage {
  private readonly api = inject(BillingApiService);

  protected readonly permissions = BillingPermissions;
  protected readonly sources = SOURCE_LABELS;
  protected readonly badgeOf = invoiceBadge;
  protected readonly statuses: InvoiceStatus[] = ['Draft', 'Finalized', 'Cancelled'];
  protected readonly columns = ['number', 'patient', 'net', 'due', 'status'];
  protected readonly from = signal<Date | null>(new Date());
  protected readonly to = signal<Date | null>(new Date());
  protected readonly query = signal<InvoiceQuery>({
    pageNumber: 1,
    pageSize: 50,
    dateFrom: toIsoDate(new Date())!,
    dateTo: toIsoDate(new Date())!,
  });
  protected readonly page = signal<PagedResponse<InvoiceSummary> | null>(null);
  protected readonly loading = signal(false);
  protected readonly search$ = new Subject<string>();

  constructor() {
    this.search$.pipe(debounceTime(300), takeUntilDestroyed()).subscribe((search) => this.patch({ search: search.trim() || undefined }));
    toObservable(this.query)
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap((q) => this.api.invoices(q).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.page.set(page);
        this.loading.set(false);
      });
  }

  protected patch(change: Partial<InvoiceQuery>): void {
    this.query.update((q) => ({ ...q, ...change, pageNumber: 1 }));
  }

  protected onFrom(value: Date | null): void {
    this.from.set(value);
    if (!value) {
      this.applyRange();
    }
  }

  protected onTo(value: Date | null): void {
    this.to.set(value);
    // The range picker clears the end date while a new start is chosen; search once the range is complete.
    if (value || !this.from()) {
      this.applyRange();
    }
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  private applyRange(): void {
    this.patch({ dateFrom: toIsoDate(this.from()) ?? undefined, dateTo: toIsoDate(this.to()) ?? undefined });
  }
}
