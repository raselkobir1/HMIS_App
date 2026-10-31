import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { AuthService } from '../../../core/auth/auth.service';
import { BrandingService } from '../../../core/services/branding.service';
import { NotificationService } from '../../../core/services/notification.service';
import { FieldError } from '../../../shared/components/field-error/field-error';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage, applyServerErrors } from '../../../shared/utilities/server-errors';
import { AdministrationApiService } from '../administration-api.service';
import { AdminPermissions, HospitalProfile } from '../administration.models';

@Component({
  selector: 'app-hospital-profile-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeader,
    FieldError,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Hospital Profile" subtitle="Name, registration and contact details" />
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="layout">
      <mat-card appearance="outlined">
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="save()" novalidate class="grid">
            @if (bannerError(); as message) {
              <div class="banner wide" role="alert">{{ message }}</div>
            }
            <mat-form-field class="wide">
              <mat-label>Hospital name</mat-label>
              <input matInput formControlName="name" />
              <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Name (Bangla)</mat-label>
              <input matInput formControlName="nameBangla" lang="bn" />
            </mat-form-field>
            <mat-form-field>
              <mat-label>Short name</mat-label>
              <input matInput formControlName="shortName" />
              <mat-hint>Shown in the menu header</mat-hint>
            </mat-form-field>
            <mat-form-field class="wide">
              <mat-label>Address</mat-label>
              <textarea matInput formControlName="address" rows="2"></textarea>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Phone</mat-label>
              <input matInput type="tel" formControlName="phone" />
            </mat-form-field>
            <mat-form-field>
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" />
              <mat-error><app-field-error [control]="form.controls.email" /></mat-error>
            </mat-form-field>
            <mat-form-field class="wide">
              <mat-label>Website</mat-label>
              <input matInput type="url" formControlName="website" placeholder="https://" />
              <mat-error><app-field-error [control]="form.controls.website" /></mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Registration number</mat-label>
              <input matInput formControlName="registrationNumber" />
            </mat-form-field>
            <mat-form-field>
              <mat-label>License number</mat-label>
              <input matInput formControlName="licenseNumber" />
            </mat-form-field>
            <mat-form-field>
              <mat-label>BIN / Tax ID</mat-label>
              <input matInput formControlName="taxId" />
            </mat-form-field>
            <mat-form-field class="wide">
              <mat-label>Opening hours</mat-label>
              <textarea matInput formControlName="openingHours" rows="2" placeholder="e.g. OPD: Sat–Thu 9:00–21:00; Emergency: 24/7"></textarea>
            </mat-form-field>
            @if (canManage) {
              <div class="wide actions">
                <button mat-flat-button type="submit" [disabled]="saving() || form.pristine">Save changes</button>
              </div>
            }
          </form>
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined" class="logo-card">
        <mat-card-header><mat-card-title>Logo</mat-card-title></mat-card-header>
        <mat-card-content>
          <div class="logo-box">
            @if (logoUrl(); as url) {
              <img [src]="url" alt="Hospital logo" />
            } @else {
              <mat-icon class="placeholder">local_hospital</mat-icon>
              <span class="hint">No logo uploaded</span>
            }
          </div>
          <p class="hint">PNG, JPEG or WebP, up to 1 MB.</p>
          @if (canManage) {
            <input #fileInput type="file" accept="image/png,image/jpeg,image/webp" hidden (change)="upload(fileInput)" />
            <div class="logo-actions">
              <button mat-stroked-button type="button" [disabled]="uploading()" (click)="fileInput.click()">
                <mat-icon>upload</mat-icon>
                {{ profile()?.hasLogo ? 'Replace' : 'Upload' }}
              </button>
              @if (profile()?.hasLogo) {
                <button mat-button type="button" [disabled]="uploading()" (click)="removeLogo()">Remove</button>
              }
            </div>
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 280px;
      gap: 16px;
      align-items: start;
    }
    .grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 4px 12px;
      padding-top: 8px;
    }
    .wide {
      grid-column: 1 / -1;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
    .banner {
      padding: 12px;
      border-radius: 8px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .logo-box {
      display: grid;
      place-items: center;
      height: 160px;
      border: 1px dashed var(--mat-sys-outline-variant);
      border-radius: 8px;
      margin-top: 8px;
      overflow: hidden;
    }
    .logo-box img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }
    .placeholder {
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: var(--mat-sys-outline);
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .logo-actions {
      display: flex;
      gap: 8px;
    }
    @media (max-width: 900px) {
      .layout {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 600px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class HospitalProfilePage {
  private readonly api = inject(AdministrationApiService);
  private readonly notifications = inject(NotificationService);
  private readonly branding = inject(BrandingService);

  protected readonly canManage = inject(AuthService).hasPermission(AdminPermissions.profileManage);
  protected readonly profile = signal<HospitalProfile | null>(null);
  protected readonly logoUrl = this.branding.logoUrl;
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly uploading = signal(false);
  protected readonly bannerError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    nameBangla: ['', Validators.maxLength(200)],
    shortName: ['', Validators.maxLength(50)],
    address: ['', Validators.maxLength(500)],
    phone: ['', Validators.maxLength(50)],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    website: ['', [Validators.maxLength(256), Validators.pattern(/^https?:\/\/.+/)]],
    registrationNumber: ['', Validators.maxLength(100)],
    licenseNumber: ['', Validators.maxLength(100)],
    taxId: ['', Validators.maxLength(50)],
    openingHours: ['', Validators.maxLength(500)],
  });

  constructor() {
    if (!this.canManage) {
      this.form.disable();
    }

    this.api.getProfile().subscribe({
      next: (profile) => {
        this.setProfile(profile);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.bannerError.set(apiErrorMessage(error, 'The hospital profile could not be loaded.'));
      },
    });
  }

  protected save(): void {
    const profile = this.profile();
    if (!profile || this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.bannerError.set(null);
    const value = this.form.getRawValue();
    const nullIfEmpty = (v: string) => (v.trim() ? v : null);

    this.api
      .updateProfile({
        name: value.name,
        nameBangla: nullIfEmpty(value.nameBangla),
        shortName: nullIfEmpty(value.shortName),
        address: nullIfEmpty(value.address),
        phone: nullIfEmpty(value.phone),
        email: nullIfEmpty(value.email),
        website: nullIfEmpty(value.website),
        registrationNumber: nullIfEmpty(value.registrationNumber),
        licenseNumber: nullIfEmpty(value.licenseNumber),
        taxId: nullIfEmpty(value.taxId),
        openingHours: nullIfEmpty(value.openingHours),
        rowVersion: profile.rowVersion,
      })
      .subscribe({
        next: (updated) => {
          this.setProfile(updated);
          this.saving.set(false);
          this.branding.refresh();
          this.notifications.success('Hospital profile saved.');
        },
        error: (error) => {
          this.saving.set(false);
          this.bannerError.set(applyServerErrors(this.form, error));
        },
      });
  }

  protected upload(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > 1024 * 1024) {
      this.notifications.error('The logo must be at most 1 MB.');
      return;
    }

    this.uploading.set(true);
    this.api.uploadLogo(file).subscribe({
      next: (profile) => this.afterLogoChange(profile, 'Logo updated.'),
      error: (error) => {
        this.uploading.set(false);
        this.notifications.error(apiErrorMessage(error, 'The logo could not be uploaded.'));
      },
    });
  }

  protected removeLogo(): void {
    this.uploading.set(true);
    this.api.removeLogo().subscribe({
      next: (profile) => this.afterLogoChange(profile, 'Logo removed.'),
      error: (error) => {
        this.uploading.set(false);
        this.notifications.error(apiErrorMessage(error));
      },
    });
  }

  private afterLogoChange(profile: HospitalProfile, message: string): void {
    this.profile.set(profile);
    this.uploading.set(false);
    this.branding.refresh();
    this.notifications.success(message);
  }

  private setProfile(profile: HospitalProfile): void {
    this.profile.set(profile);
    this.form.reset({
      name: profile.name,
      nameBangla: profile.nameBangla ?? '',
      shortName: profile.shortName ?? '',
      address: profile.address ?? '',
      phone: profile.phone ?? '',
      email: profile.email ?? '',
      website: profile.website ?? '',
      registrationNumber: profile.registrationNumber ?? '',
      licenseNumber: profile.licenseNumber ?? '',
      taxId: profile.taxId ?? '',
      openingHours: profile.openingHours ?? '',
    });
  }
}
