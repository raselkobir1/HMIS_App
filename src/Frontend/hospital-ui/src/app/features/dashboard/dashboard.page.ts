import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

import { AuthService } from '../../core/auth/auth.service';
import { PageHeader } from '../../shared/components/page-header/page-header';

@Component({
  selector: 'app-dashboard-page',
  imports: [MatCardModule, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Dashboard" [subtitle]="'Welcome, ' + (user()?.fullName ?? 'User')" />
    <mat-card appearance="outlined">
      <mat-card-content>
        Operational widgets will appear here as modules are delivered (patients, appointments, beds, revenue).
      </mat-card-content>
    </mat-card>
  `,
})
export class DashboardPage {
  protected readonly user = inject(AuthService).user;
}
