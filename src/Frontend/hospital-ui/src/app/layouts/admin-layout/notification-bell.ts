import { DatePipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';

interface AppNotification {
  id: string;
  type: string;
  title: string;
  message: string;
  link: string | null;
  createdAt: string;
  isRead: boolean;
}

const POLL_MS = 60_000;

const ICONS: Record<string, string> = {
  AppointmentBooked: 'event',
  PatientAdmitted: 'hotel',
  DischargeOrdered: 'assignment_turned_in',
  FinalBillReady: 'receipt_long',
  PatientDischarged: 'logout',
  DiscountApproval: 'percent',
  RefundApproval: 'currency_exchange',
};

/** Toolbar bell: unread count (checked every minute), the latest notifications, mark read and open the linked screen. */
@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatMenuModule, MatBadgeModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button mat-icon-button type="button" [matMenuTriggerFor]="menu" (menuOpened)="load()"
            [attr.aria-label]="unread() ? unread() + ' unread notifications' : 'Notifications'">
      <mat-icon [matBadge]="unread() || null" matBadgeColor="warn" matBadgeSize="small" aria-hidden="false">notifications</mat-icon>
    </button>
    <mat-menu #menu="matMenu" xPosition="before" class="notification-menu">
      <div class="head" (click)="$event.stopPropagation()" (keydown)="$event.stopPropagation()">
        <strong>Notifications</strong>
        @if (unread()) {
          <button mat-button type="button" (click)="readAll()">Mark all read</button>
        }
      </div>
      @for (n of items(); track n.id) {
        <button mat-menu-item type="button" class="item" [class.unread]="!n.isRead" (click)="open(n)">
          <mat-icon>{{ icon(n.type) }}</mat-icon>
          <span class="text">
            <span class="title">{{ n.title }}</span>
            <span class="message">{{ n.message }}</span>
            <span class="time">{{ n.createdAt | date: 'd MMM, h:mm a' }}</span>
          </span>
        </button>
      } @empty {
        <p class="empty">{{ loaded() ? 'No notifications yet.' : 'Loading…' }}</p>
      }
    </mat-menu>
  `,
  styles: `
    .head { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 4px 8px 4px 16px; min-width: 320px; }
    .item { height: auto; min-height: 64px; padding-top: 8px; padding-bottom: 8px; max-width: 380px; }
    .item.unread { background: var(--mat-sys-secondary-container); }
    .text { display: flex; flex-direction: column; line-height: 1.3; white-space: normal; }
    .title { font: var(--mat-sys-label-large); }
    .message, .time { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    .empty { margin: 8px 16px 12px; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class NotificationBell {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = `${environment.apiBaseUrl}/notifications`;

  protected readonly unread = signal(0);
  protected readonly items = signal<AppNotification[]>([]);
  protected readonly loaded = signal(false);

  constructor() {
    this.refreshCount();
    const timer = setInterval(() => this.refreshCount(), POLL_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected icon(type: string): string {
    return ICONS[type] ?? 'notifications';
  }

  protected load(): void {
    this.http
      .get<ApiResponse<{ unreadCount: number; items: AppNotification[] }>>(this.base, { params: new HttpParams().set('take', 15) })
      .pipe(map((r) => r.data!))
      .subscribe({
        next: (list) => {
          this.items.set(list.items);
          this.unread.set(list.unreadCount);
          this.loaded.set(true);
        },
        error: () => this.loaded.set(true),
      });
  }

  protected open(n: AppNotification): void {
    if (!n.isRead) {
      this.http.post(`${this.base}/${n.id}/read`, {}).subscribe({ next: () => this.refreshCount() });
    }
    if (n.link) {
      this.router.navigateByUrl(n.link);
    }
  }

  protected readAll(): void {
    this.http.post(`${this.base}/read-all`, {}).subscribe({
      next: () => {
        this.unread.set(0);
        this.items.update((list) => list.map((n) => ({ ...n, isRead: true })));
      },
    });
  }

  private refreshCount(): void {
    this.http
      .get<ApiResponse<number>>(`${this.base}/unread-count`)
      .pipe(map((r) => r.data ?? 0))
      .subscribe({ next: (count) => this.unread.set(count), error: () => undefined });
  }
}
