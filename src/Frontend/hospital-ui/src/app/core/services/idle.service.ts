import { DestroyRef, effect, inject, Injectable, NgZone } from '@angular/core';

import { AuthService } from '../auth/auth.service';

/** Signs the user out after 30 minutes without keyboard, mouse, touch or scroll activity (Q-SEC-3). */
@Injectable({ providedIn: 'root' })
export class IdleService {
  private static readonly TIMEOUT_MS = 30 * 60 * 1000;
  private static readonly EVENTS = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll', 'wheel'];

  private readonly auth = inject(AuthService);
  private readonly zone = inject(NgZone);
  private timer: ReturnType<typeof setTimeout> | null = null;
  private lastReset = 0;
  private readonly onActivity = () => this.reset();

  constructor() {
    effect(() => (this.auth.isAuthenticated() ? this.start() : this.stop()));
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  private start(): void {
    if (this.timer) {
      return;
    }
    this.zone.runOutsideAngular(() =>
      IdleService.EVENTS.forEach((e) => window.addEventListener(e, this.onActivity, { passive: true })),
    );
    this.schedule();
  }

  private stop(): void {
    IdleService.EVENTS.forEach((e) => window.removeEventListener(e, this.onActivity));
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private reset(): void {
    // Throttle: activity events fire constantly; re-arming once every few seconds is enough.
    const now = Date.now();
    if (now - this.lastReset > 5000) {
      this.lastReset = now;
      this.schedule();
    }
  }

  private schedule(): void {
    if (this.timer) {
      clearTimeout(this.timer);
    }
    this.timer = setTimeout(() => this.zone.run(() => this.auth.logout('idle')), IdleService.TIMEOUT_MS);
  }
}
