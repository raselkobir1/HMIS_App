import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { forkJoin, Observable } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { TakaPipe } from '../../billing/components/taka.pipe';
import { BedCategoryDialog, BedDialog, WardDialog } from '../components/setup-dialogs';
import { Bed, BED_STATUS_LABELS, BedCategory, IpdApiService, IpdPermissions, Ward, WARD_TYPE_LABELS } from '../ipd.models';

/** Bed categories (rates and deposits), wards and beds. Route: /ipd/setup. */
@Component({
  selector: 'app-ipd-setup-page',
  imports: [MatTabsModule, MatButtonModule, MatIconModule, PageHeader, TakaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Ward & bed setup" subtitle="Bed categories set the daily rent and the suggested admission deposit" />
    <mat-tab-group animationDuration="0ms">
      <mat-tab [label]="'Bed categories (' + categories().length + ')'">
        <div class="toolbar">
          @if (canManage) {
            <button mat-flat-button type="button" (click)="editCategory(null)"><mat-icon>add</mat-icon> Add category</button>
          }
        </div>
        @for (c of categories(); track c.id) {
          <div class="row" [class.inactive]="!c.isActive">
            <span class="grow"><strong>{{ c.name }}</strong>{{ c.isActive ? '' : ' (inactive)' }}</span>
            <span class="num">{{ c.dailyRate | taka }}/day</span>
            <span class="num secondary">deposit {{ c.suggestedDeposit | taka }}</span>
            @if (canManage) {
              <button mat-icon-button type="button" (click)="editCategory(c)" [attr.aria-label]="'Edit ' + c.name"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button type="button" (click)="toggle(api.setCategoryActive(c.id, !c.isActive))" [attr.aria-label]="(c.isActive ? 'Deactivate ' : 'Activate ') + c.name">
                <mat-icon>{{ c.isActive ? 'toggle_on' : 'toggle_off' }}</mat-icon>
              </button>
            }
          </div>
        } @empty {
          <p class="secondary">Add bed categories first (e.g. General ward bed, Single cabin AC, ICU bed).</p>
        }
      </mat-tab>
      <mat-tab [label]="'Wards (' + wards().length + ')'">
        <div class="toolbar">
          @if (canManage) {
            <button mat-flat-button type="button" (click)="editWard(null)"><mat-icon>add</mat-icon> Add ward</button>
          }
        </div>
        @for (w of wards(); track w.id) {
          <div class="row" [class.inactive]="!w.isActive">
            <span class="grow">
              <strong>{{ w.code }}</strong> · {{ w.name }}{{ w.isActive ? '' : ' (closed)' }}
              <span class="secondary block">
                {{ wardTypes[w.type] }}{{ w.departmentName ? ' · ' + w.departmentName : '' }}{{ w.building ? ' · ' + w.building : '' }}{{ w.floor ? ', floor ' + w.floor : '' }}
              </span>
            </span>
            <span class="secondary">{{ w.bedCount }} bed{{ w.bedCount === 1 ? '' : 's' }}</span>
            @if (canManage) {
              <button mat-icon-button type="button" (click)="editWard(w)" [attr.aria-label]="'Edit ' + w.name"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button type="button" (click)="toggle(api.setWardActive(w.id, !w.isActive))" [attr.aria-label]="(w.isActive ? 'Close ' : 'Open ') + w.name">
                <mat-icon>{{ w.isActive ? 'toggle_on' : 'toggle_off' }}</mat-icon>
              </button>
            }
          </div>
        } @empty {
          <p class="secondary">No wards yet.</p>
        }
      </mat-tab>
      <mat-tab [label]="'Beds (' + beds().length + ')'">
        <div class="toolbar">
          @if (canManage) {
            <button mat-flat-button type="button" [disabled]="!wards().length || !categories().length" (click)="editBed(null)"><mat-icon>add</mat-icon> Add bed</button>
          }
        </div>
        @for (b of beds(); track b.id) {
          <div class="row" [class.inactive]="!b.isActive">
            <span class="grow">
              <strong>{{ b.wardName }} {{ b.bedNumber }}</strong>{{ b.room ? ' · room ' + b.room : '' }}{{ b.isActive ? '' : ' (inactive)' }}
              <span class="secondary block">{{ b.categoryName }} · {{ b.dailyRate | taka }}/day</span>
            </span>
            <span class="secondary">{{ statusLabels[b.status] }}</span>
            @if (canManage) {
              <button mat-icon-button type="button" (click)="editBed(b)" [attr.aria-label]="'Edit bed ' + b.bedNumber"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button type="button" (click)="toggle(api.setBedActive(b.id, !b.isActive))" [attr.aria-label]="(b.isActive ? 'Deactivate bed ' : 'Activate bed ') + b.bedNumber">
                <mat-icon>{{ b.isActive ? 'toggle_on' : 'toggle_off' }}</mat-icon>
              </button>
            }
          </div>
        } @empty {
          <p class="secondary">No beds yet.</p>
        }
      </mat-tab>
    </mat-tab-group>
  `,
  styles: `
    .toolbar { display: flex; justify-content: flex-end; padding: 12px 0; min-height: 8px; }
    .row { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 12px; padding: 8px 4px; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .row.inactive { opacity: 0.6; }
    .grow { flex: 1 1 240px; min-width: 0; }
    .num { white-space: nowrap; }
    .block { display: block; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class IpdSetupPage {
  protected readonly api = inject(IpdApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly canManage = inject(AuthService).hasPermission(IpdPermissions.setupManage);
  protected readonly wardTypes = WARD_TYPE_LABELS;
  protected readonly statusLabels = BED_STATUS_LABELS;
  protected readonly categories = signal<BedCategory[]>([]);
  protected readonly wards = signal<Ward[]>([]);
  protected readonly beds = signal<Bed[]>([]);

  constructor() {
    this.load();
  }

  protected editCategory(category: BedCategory | null): void {
    this.afterSave(this.dialog.open(BedCategoryDialog, { data: category, width: '520px' }).afterClosed());
  }

  protected editWard(ward: Ward | null): void {
    this.afterSave(this.dialog.open(WardDialog, { data: ward, width: '560px' }).afterClosed());
  }

  protected editBed(bed: Bed | null): void {
    this.afterSave(
      this.dialog.open(BedDialog, { data: { bed, wards: this.wards(), categories: this.categories() }, width: '520px' }).afterClosed(),
    );
  }

  protected toggle(request: Observable<unknown>): void {
    request.subscribe({ next: () => this.load(), error: (err) => this.notifications.error(apiErrorMessage(err)) });
  }

  private afterSave(closed: Observable<unknown>): void {
    closed.subscribe((saved) => saved && this.load());
  }

  private load(): void {
    forkJoin({ categories: this.api.categories(), wards: this.api.wards(), beds: this.api.beds({}) }).subscribe({
      next: ({ categories, wards, beds }) => {
        this.categories.set(categories);
        this.wards.set(wards);
        this.beds.set(beds);
      },
      error: (err) => this.notifications.error(apiErrorMessage(err, 'The setup could not be loaded.')),
    });
  }
}
