import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
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
import { ADMISSION_STATUS_LABELS, AdmissionQuery, AdmissionStatus, AdmissionSummary, IpdApiService, IpdPermissions, Ward } from '../ipd.models';

/** Patients in hospital (default) and past admissions. Route: /ipd/admissions. */
@Component({
  selector: 'app-admissions-page',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
    HasPermissionDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Admissions" subtitle="Inpatients and their stays">
      <a mat-flat-button routerLink="/ipd/admit" *hasPermission="permissions.admit"><mat-icon>add</mat-icon> Admit patient</a>
    </app-page-header>
    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Admission number</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input matInput [ngModel]="query().search ?? ''" (ngModelChange)="search$.next($event)" />
      </mat-form-field>
      <mat-form-field>
        <mat-label>Show</mat-label>
        <mat-select [ngModel]="query().status ?? 'current'" (ngModelChange)="onStatus($event)">
          <mat-option value="current">In hospital now</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ labels[s] }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Ward</mat-label>
        <mat-select [ngModel]="query().wardId ?? ''" (ngModelChange)="patch({ wardId: $event || undefined })">
          <mat-option value="">All wards</mat-option>
          @for (w of wards(); track w.id) {
            <mat-option [value]="w.id">{{ w.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </section>
    <div class="table-card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="table-scroll">
        <table mat-table [dataSource]="page()?.items ?? []">
          <ng-container matColumnDef="patient">
            <th mat-header-cell *matHeaderCellDef>Patient</th>
            <td mat-cell *matCellDef="let a">
              <a [routerLink]="['/ipd/admissions', a.id]">{{ a.patientName ?? 'Unknown' }}</a>
              <div class="secondary">{{ a.patientMRNumber }} · {{ a.admissionNumber }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="bed">
            <th mat-header-cell *matHeaderCellDef>Ward / bed</th>
            <td mat-cell *matCellDef="let a">{{ a.wardName }}<div class="secondary">Bed {{ a.bedNumber }}</div></td>
          </ng-container>
          <ng-container matColumnDef="doctor">
            <th mat-header-cell *matHeaderCellDef>Doctor</th>
            <td mat-cell *matCellDef="let a">{{ a.doctorName }}</td>
          </ng-container>
          <ng-container matColumnDef="stay">
            <th mat-header-cell *matHeaderCellDef>Stay</th>
            <td mat-cell *matCellDef="let a">
              {{ a.admittedAt | date: 'd MMM y, h:mm a' }}
              <div class="secondary">{{ a.days }} day{{ a.days === 1 ? '' : 's' }}{{ a.dischargedAt ? ' · discharged ' + (a.dischargedAt | date: 'd MMM') : '' }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let a">
              <span class="badge" [class.ok]="a.status === 'Admitted'" [class.info]="a.status === 'DischargeOrdered' || a.status === 'FinalBilled'"
                    [class.muted]="a.status === 'Discharged'">{{ labels[a.status] }}</span>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-row" *matNoDataRow>
            <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No admissions for these filters.' }}</td>
          </tr>
        </table>
      </div>
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="query().pageNumber - 1" [pageSize]="query().pageSize"
                     [pageSizeOptions]="[25, 50, 100]" (page)="onPage($event)" aria-label="Admission pages" />
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class AdmissionsPage {
  private readonly api = inject(IpdApiService);

  protected readonly permissions = IpdPermissions;
  protected readonly labels: Record<string, string> = ADMISSION_STATUS_LABELS;
  protected readonly statuses = Object.keys(ADMISSION_STATUS_LABELS) as AdmissionStatus[];
  protected readonly columns = ['patient', 'bed', 'doctor', 'stay', 'status'];
  protected readonly wards = toSignal(this.api.wards(), { initialValue: [] as Ward[] });
  protected readonly query = signal<AdmissionQuery>({ pageNumber: 1, pageSize: 50 });
  protected readonly page = signal<PagedResponse<AdmissionSummary> | null>(null);
  protected readonly loading = signal(false);
  protected readonly search$ = new Subject<string>();

  constructor() {
    this.search$.pipe(debounceTime(300), takeUntilDestroyed()).subscribe((search) => this.patch({ search: search.trim() || undefined }));
    toObservable(this.query)
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap((q) => this.api.admissions(q).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.page.set(page);
        this.loading.set(false);
      });
  }

  protected patch(change: Partial<AdmissionQuery>): void {
    this.query.update((q) => ({ ...q, ...change, pageNumber: 1 }));
  }

  protected onStatus(value: AdmissionStatus | 'current'): void {
    this.patch({ status: value === 'current' ? undefined : value });
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }
}
