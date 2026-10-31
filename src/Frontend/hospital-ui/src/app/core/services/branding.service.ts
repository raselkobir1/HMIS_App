import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

interface Branding {
  name: string;
  shortName: string | null;
  hasLogo: boolean;
  logoVersion: string | null;
}

/** Public hospital name and logo for the login page and the app shell. */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);
  private readonly branding = signal<Branding>({ name: 'Hospital Management System', shortName: 'HMIS', hasLogo: false, logoVersion: null });

  readonly name = computed(() => this.branding().name);
  readonly shortName = computed(() => this.branding().shortName || this.branding().name);
  readonly logoUrl = computed(() =>
    this.branding().hasLogo ? `${environment.apiBaseUrl}/hospital-profile/logo?v=${this.branding().logoVersion}` : null,
  );

  load(): Observable<unknown> {
    return this.http.get<ApiResponse<Branding>>(`${environment.apiBaseUrl}/hospital-profile/branding`).pipe(
      map((r) => r.data!),
      tap((branding) => this.branding.set(branding)),
      catchError(() => of(null)),
    );
  }

  /** Called after the profile or logo changes so the shell updates without a reload. */
  refresh(): void {
    this.load().subscribe();
  }
}
