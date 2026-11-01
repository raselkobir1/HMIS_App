import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { map } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import { NotificationService } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';

interface NotificationSetting {
  type: string;
  label: string;
  recipients: string;
  isEnabled: boolean;
}

/** Switch each in-app notification type on or off for the whole hospital. Route: /administration/notifications. */
@Component({
  selector: 'app-notification-settings-page',
  imports: [MatCardModule, MatSlideToggleModule, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Notification Settings" subtitle="In-app notifications shown under the bell" />
    @if (error(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    }
    <mat-card appearance="outlined">
      <mat-card-content>
        @for (s of settings(); track s.type) {
          <div class="row">
            <div class="grow">
              <strong>{{ s.label }}</strong>
              <div class="secondary">Sent to: {{ s.recipients }}</div>
            </div>
            <mat-slide-toggle [checked]="s.isEnabled" (change)="toggle(s, $event.checked)" [aria-label]="s.label" />
          </div>
        } @empty {
          <p class="secondary">Loading…</p>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; max-width: 720px; }
    .row { display: flex; align-items: center; gap: 12px; padding: 12px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .row:last-child { border-bottom: 0; }
    .grow { flex: 1; }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .banner { padding: 10px 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
  `,
})
export class NotificationSettingsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notifications = inject(NotificationService);
  private readonly base = `${environment.apiBaseUrl}/notifications/settings`;

  protected readonly settings = signal<NotificationSetting[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.http
      .get<ApiResponse<NotificationSetting[]>>(this.base)
      .pipe(map((r) => r.data ?? []))
      .subscribe({ next: (list) => this.settings.set(list), error: (err) => this.error.set(apiErrorMessage(err)) });
  }

  protected toggle(setting: NotificationSetting, isEnabled: boolean): void {
    this.http.put<ApiResponse<NotificationSetting>>(`${this.base}/${setting.type}`, { isEnabled }).subscribe({
      next: () => {
        this.settings.update((list) => list.map((s) => (s.type === setting.type ? { ...s, isEnabled } : s)));
        this.notifications.success(`${setting.label}: ${isEnabled ? 'on' : 'off'}.`);
      },
      error: (err) => {
        this.notifications.error(apiErrorMessage(err));
        this.settings.update((list) => [...list]);
      },
    });
  }
}
