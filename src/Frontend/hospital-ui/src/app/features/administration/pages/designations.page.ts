import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap } from 'rxjs';

import { PagedResponse } from '../../../core/models/api-response.model';
import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../administration-api.service';
import { AdminPermissions, Designation, DESIGNATION_CATEGORIES, DesignationCategory, DesignationQuery } from '../administration.models';

@Component({
  selector: 'app-designations-page',
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
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
    <app-page-header title="Designations" subtitle="Job titles used for staff records">
      <button mat-flat-button type="button" *hasPermission="permissions.designationsManage" (click)="openForm(null)">
        <mat-icon>add</mat-icon>
        New designation
      </button>
    </app-page-header>

    <section class="filters">
      <mat-form-field class="search">
        <mat-label>Search</mat-label>
        <input matInput [formControl]="search" />
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
      <mat-form-field>
        <mat-label>Category</mat-label>
        <mat-select [formControl]="category">
          <mat-option value="">All</mat-option>
          @for (c of categories; track c) {
            <mat-option [value]="c">{{ c }}</mat-option>
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
        <div class="state error" role="alert"><mat-icon>error</mat-icon><span>{{ error }}</span></div>
      } @else {
        <div class="table-scroll">
          <table mat-table [dataSource]="page()?.items ?? []">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Designation</th>
              <td mat-cell *matCellDef="let d">{{ d.name }}</td>
            </ng-container>
            <ng-container matColumnDef="category">
              <th mat-header-cell *matHeaderCellDef>Category</th>
              <td mat-cell *matCellDef="let d">{{ d.category }}</td>
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
                <ng-container *hasPermission="permissions.designationsManage">
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
              <td class="state" [attr.colspan]="columns.length">{{ loading() ? 'Loading…' : 'No designations found.' }}</td>
            </tr>
          </table>
        </div>
        <mat-paginator
          [length]="page()?.totalCount ?? 0"
          [pageIndex]="query().pageNumber - 1"
          [pageSize]="query().pageSize"
          [pageSizeOptions]="[20, 50, 100]"
          (page)="onPage($event)"
          aria-label="Designation pages"
        />
      }
    </div>
  `,
  styleUrl: '../../../shared/styles/list-page.scss',
})
export class DesignationsPage {
  private readonly api = inject(AdministrationApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly permissions = AdminPermissions;
  protected readonly categories = DESIGNATION_CATEGORIES;
  protected readonly columns = ['name', 'category', 'status', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly category = new FormControl<DesignationCategory | ''>('', { nonNullable: true });
  protected readonly status = new FormControl<'all' | 'active' | 'inactive'>('all', { nonNullable: true });
  protected readonly query = signal<DesignationQuery>({ pageNumber: 1, pageSize: 50 });
  protected readonly page = signal<PagedResponse<Designation> | null>(null);
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
          this.api.searchDesignations(q).pipe(
            catchError((error: unknown) => {
              this.loadError.set(apiErrorMessage(error, 'Designations could not be loaded.'));
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
    this.category.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((category) => this.query.update((q) => ({ ...q, category: category || undefined, pageNumber: 1 })));
    this.status.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((s) => this.query.update((q) => ({ ...q, isActive: s === 'all' ? undefined : s === 'active', pageNumber: 1 })));
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected openForm(designation: Designation | null): void {
    this.dialog
      .open(DesignationFormDialog, { data: designation, width: '440px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => this.query.update((q) => ({ ...q })));
  }

  protected toggle(designation: Designation): void {
    this.api.setDesignationStatus(designation.id, !designation.isActive).subscribe({
      next: () => {
        this.notifications.success(designation.isActive ? 'Designation deactivated.' : 'Designation activated.');
        this.query.update((q) => ({ ...q }));
      },
      error: (error) => this.notifications.error(apiErrorMessage(error)),
    });
  }
}

@Component({
  selector: 'app-designation-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, FieldError],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ designation ? 'Edit designation' : 'New designation' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (bannerError(); as message) {
          <div class="banner" role="alert">{{ message }}</div>
        }
        <mat-form-field class="full">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" />
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Category</mat-label>
          <mat-select formControlName="category">
            @for (c of categories; track c) {
              <mat-option [value]="c">{{ c }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ designation ? 'Save' : 'Create' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styleUrl: '../../../shared/styles/dialog.scss',
})
export class DesignationFormDialog {
  protected readonly designation = inject<Designation | null>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DesignationFormDialog, boolean>);
  private readonly api = inject(AdministrationApiService);
  private readonly notifications = inject(NotificationService);

  protected readonly categories = DESIGNATION_CATEGORIES;
  protected readonly saving = signal(false);
  protected readonly bannerError = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.designation?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    category: [this.designation?.category ?? ('Other' as DesignationCategory), Validators.required],
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const request$ = this.designation
      ? this.api.updateDesignation(this.designation.id, { ...value, rowVersion: this.designation.rowVersion })
      : this.api.createDesignation(value);

    request$.subscribe({
      next: () => {
        this.notifications.success(this.designation ? 'Designation updated.' : 'Designation created.');
        this.dialogRef.close(true);
      },
      error: (error) => {
        this.saving.set(false);
        this.bannerError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
