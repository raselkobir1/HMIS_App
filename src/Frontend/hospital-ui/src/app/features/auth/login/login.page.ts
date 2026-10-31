import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { ApiResponse } from '../../../core/models/api-response.model';

@Component({
  selector: 'app-login-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly infoMessage =
    this.route.snapshot.queryParamMap.get('reason') === 'idle' ? 'You were signed out after 30 minutes of inactivity.' : null;
  protected readonly hidePassword = signal(true);

  protected readonly form = inject(NonNullableFormBuilder).group({
    userNameOrEmail: ['', [Validators.required, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
  });

  protected submit(): void {
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (user) => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        void this.router.navigateByUrl(user.mustChangePassword ? '/change-password' : returnUrl);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        const body = error.error as ApiResponse<unknown> | null;
        this.errorMessage.set(
          error.status === 0 ? 'Cannot reach the server.' : (body?.message ?? 'Sign in failed. Please try again.'),
        );
      },
    });
  }
}
