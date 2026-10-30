import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-authentication-layout',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="auth-shell">
      <section class="auth-card-wrapper">
        <header class="brand">
          <span class="material-symbols-outlined brand-icon" aria-hidden="true">local_hospital</span>
          <h1>Hospital Management System</h1>
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
    .brand-icon {
      font-size: 36px;
    }
  `,
})
export class AuthenticationLayout {}
