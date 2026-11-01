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
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../../core/models/api-response.model';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { PatientApiService } from '../patient-api.service';
import { ageLabel, PatientListItem, PatientPermissions, PatientSearchQuery, PatientStatus } from '../patient.models';

@Component({
  selector: 'app-patient-search-page',
  imports: [
    ReactiveFormsModule,
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
    <app-page-header title="Patients" subtitle="Search by MR number, mobile, name or ID number">
      <a mat-flat-button routerLink="new" *hasPermission="permissions.create">
        <mat-icon>person_add</mat-icon>
        Register patient
      </a>
    </app-page-header>

    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Search patients</mat-label>
        <input matInput [formControl]="search" placeholder="e.g. MR26000012, 01711…, Rahim" autofocus />
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Status</mat-label>
        <mat-select [formControl]="status">
          <mat-option value="">All</mat-option>
          <mat-option value="Active">Active</mat-option>
          <mat-option value="Inactive">Inactive</mat-option>
          <mat-option value="Merged">Merged</mat-option>
          <mat-option value="Deceased">Deceased</mat-option>
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
            <ng-container matColumnDef="mrNumber">
              <th mat-header-cell *matHeaderCellDef>MR number</th>
              <td mat-cell *matCellDef="let p"><strong>{{ p.mrNumber }}</strong></td>
            </ng-container>
            <ng-container matColumnDef="fullName">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let p">{{ p.fullName }}</td>
            </ng-container>
            <ng-container matColumnDef="ageSex">
              <th mat-header-cell *matHeaderCellDef>Age / Sex</th>
              <td mat-cell *matCellDef="let p">{{ age(p) }} / {{ p.gender }}</td>
            </ng-container>
            <ng-container matColumnDef="mobileNumber">
              <th mat-header-cell *matHeaderCellDef>Mobile</th>
              <td mat-cell *matCellDef="let p">{{ p.mobileNumber }}</td>
            </ng-container>
            <ng-container matColumnDef="district">
              <th mat-header-cell *matHeaderCellDef>District</th>
              <td mat-cell *matCellDef="let p">{{ p.district ?? '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let p">
                <span class="badge" [class.ok]="p.status === 'Active'" [class.muted]="p.status !== 'Active'">{{ p.status }}</span>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
            <tr mat-row *matRowDef="let row; columns: columns" class="clickable" tabindex="0"
                (click)="open(row)" (keydown.enter)="open(row)"></tr>
            <tr class="mat-row" *matNoDataRow>
              <td class="state" [attr.colspan]="columns.length">
                {{ loading() ? 'Searching…' : query().search ? 'No patients found. Check the spelling or register a new patient.' : 'No patients registered yet.' }}
              </td>
            </tr>
          </table>
        </div>
        <mat-paginator
          [length]="page()?.totalCount ?? 0"
          [pageIndex]="query().pageNumber - 1"
          [pageSize]="query().pageSize"
          [pageSizeOptions]="[10, 20, 50]"
          (page)="onPage($event)"
          aria-label="Patient pages"
        />
      }
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
  styles: `
    .clickable {
      cursor: pointer;
      &:hover,
      &:focus-visible {
        background: var(--mat-sys-surface-container);
        outline: none;
      }
    }
  `,
})
export class PatientSearchPage {
  private readonly api = inject(PatientApiService);
  private readonly router = inject(Router);

  protected readonly permissions = PatientPermissions;
  protected readonly columns = ['mrNumber', 'fullName', 'ageSex', 'mobileNumber', 'district', 'status'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<PatientStatus | ''>('', { nonNullable: true });
  protected readonly query = signal<PatientSearchQuery>({ pageNumber: 1, pageSize: 20 });
  protected readonly page = signal<PagedResponse<PatientListItem> | null>(null);
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
          this.api.search(q).pipe(
            catchError((error: unknown) => {
              this.loadError.set(apiErrorMessage(error, 'Patients could not be loaded.'));
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
    this.status.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((status) => this.query.update((q) => ({ ...q, status: status || undefined, pageNumber: 1 })));
  }

  protected age(p: PatientListItem): string {
    return ageLabel(p.dateOfBirth, p.isDateOfBirthEstimated);
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected open(patient: PatientListItem): void {
    void this.router.navigate(['/patients', patient.id]);
  }
}
