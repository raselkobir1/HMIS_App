import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { NAVIGATION, NavItem } from '../../core/navigation/navigation';

@Component({
  selector: 'app-admin-layout',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-layout.html',
  styleUrl: './admin-layout.scss',
})
export class AdminLayout {
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.user;

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe([Breakpoints.Handset, Breakpoints.TabletPortrait])
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  /** Menu filtered by the user's permissions (UI only — the API enforces access). */
  protected readonly menu = computed(() => this.filter(NAVIGATION));

  protected logout(): void {
    this.auth.logout();
  }

  private filter(items: NavItem[]): NavItem[] {
    // Reading permissions() inside computed() keeps the menu reactive to permission changes.
    this.auth.permissions();
    return items
      .filter((item) => this.auth.hasAnyPermission(item.permissions))
      .map((item) => ({ ...item, children: item.children ? this.filter(item.children) : undefined }))
      .filter((item) => item.route || (item.children?.length ?? 0) > 0);
  }
}
