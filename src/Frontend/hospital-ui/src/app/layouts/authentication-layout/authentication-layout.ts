import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { BrandingService } from '../../core/services/branding.service';

@Component({
  selector: 'app-authentication-layout',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="auth-shell">
      <section class="auth-card-wrapper">
        <header class="brand">
          @if (branding.logoUrl(); as logo) {
            <img [src]="logo" alt="" class="brand-logo" />
          } @else {
            <span class="material-symbols-outlined brand-icon" aria-hidden="true">local_hospital</span>
          }
          <h1>{{ branding.name() }}</h1>
        </header>
        <router-outlet />
      </section>
    </main>
  `,
  styles: `
    .auth-shell {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 16px;
      background: var(--mat-sys-surface-container);
    }
    .auth-card-wrapper {
      width: 100%;
      max-width: 420px;
    }
    .brand {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      margin-bottom: 24px;
      color: var(--mat-sys-primary);
    }
    .brand h1 {
      font: var(--mat-sys-headline-small);
      margin: 0;
    }
    .brand-logo {
      width: 48px;
      height: 48px;
      object-fit: contain;
    }
    .brand-icon {
      font-size: 36px;
    }
  `,
})
export class AuthenticationLayout {
  protected readonly branding = inject(BrandingService);
}
