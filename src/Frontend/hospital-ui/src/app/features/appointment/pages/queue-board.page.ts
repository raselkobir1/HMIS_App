import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import { BrandingService } from '../../../core/services/branding.service';
import { AppointmentApiService, formatTime, QueueBoard } from '../appointment.models';

const REFRESH_MS = 15_000;

/**
 * Waiting-room display (TV). Full screen, outside the admin layout. Shows serial numbers only — never patient names.
 * Route: /queue-display.
 */
@Component({
  selector: 'app-queue-board-page',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <h1>{{ branding.name() }}</h1>
      <div class="clock">
        @if (stale()) {
          <mat-icon aria-label="Connection lost">cloud_off</mat-icon>
        }
        {{ now() }}
      </div>
    </header>
    <main>
      @for (s of board()?.sessions ?? []; track s.scheduleId) {
        <section class="session">
          <div class="doctor">
            <div class="name">{{ s.doctorName }}</div>
            <div class="meta">{{ s.specialization }}{{ s.room ? ' · ' + s.room : '' }} · {{ formatTime(s.startTime) }} – {{ formatTime(s.endTime) }}</div>
          </div>
          <div class="serving">
            <span class="label">Now serving</span>
            <span class="number">{{ s.nowServing ?? '—' }}</span>
          </div>
          <div class="waiting">
            <span class="label">Next</span>
            <span class="serials">
              @for (n of s.waiting.slice(0, 8); track n) {
                <span class="serial">{{ n }}</span>
              } @empty {
                <span class="none">—</span>
              }
            </span>
          </div>
        </section>
      } @empty {
        <p class="empty">{{ board() ? 'No sessions are running right now.' : 'Loading…' }}</p>
      }
    </main>
  `,
  styles: `
    :host {
      display: flex; flex-direction: column; min-height: 100vh; padding: 24px 32px; box-sizing: border-box;
      background: var(--mat-sys-surface-container-lowest); color: var(--mat-sys-on-surface);
    }
    header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { margin: 0; font: var(--mat-sys-display-small); }
    .clock { display: flex; align-items: center; gap: 8px; font: var(--mat-sys-headline-medium); }
    main { display: grid; grid-template-columns: repeat(auto-fit, minmax(420px, 1fr)); gap: 20px; }
    .session {
      display: grid; grid-template-columns: 1fr auto; gap: 12px 24px; padding: 20px 24px; border-radius: 20px;
      background: var(--mat-sys-surface-container); border: 1px solid var(--mat-sys-outline-variant);
    }
    .doctor { grid-column: 1 / -1; }
    .name { font: var(--mat-sys-headline-small); }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-title-small); }
    .label { display: block; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-label-large); text-transform: uppercase; }
    .serving .number { font-size: 88px; line-height: 1; font-weight: 600; color: var(--mat-sys-primary); }
    .serials { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 8px; }
    .serial {
      min-width: 48px; padding: 6px 10px; border-radius: 10px; text-align: center; font: var(--mat-sys-headline-small);
      background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container);
    }
    .none, .empty { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-headline-small); }
  `,
})
export class QueueBoardPage {
  private readonly api = inject(AppointmentApiService);
  protected readonly branding = inject(BrandingService);

  protected readonly formatTime = formatTime;
  protected readonly board = signal<QueueBoard | null>(null);
  protected readonly stale = signal(false);
  protected readonly now = signal(this.clock());

  constructor() {
    this.load();
    const refresh = setInterval(() => this.load(), REFRESH_MS);
    const tick = setInterval(() => this.now.set(this.clock()), 1_000);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(refresh);
      clearInterval(tick);
    });
  }

  private load(): void {
    this.api.queueBoard().subscribe({
      next: (b) => {
        this.board.set(b);
        this.stale.set(false);
      },
      // Keep showing the last board; flag that it may be out of date.
      error: () => this.stale.set(true),
    });
  }

  private clock(): string {
    return new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Dhaka' });
  }
}
