import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';

import { ApiResponse } from '../../core/models/api-response.model';

/**
 * Maps API field errors (400/422) onto matching form controls as a `server` error.
 * Returns the message for errors that have no matching control, to show in a banner.
 */
export function applyServerErrors(form: FormGroup, error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) {
    return 'An unexpected error occurred.';
  }

  const body = error.error as ApiResponse<unknown> | null;
  if (!body?.errors?.length) {
    return body?.message ?? null;
  }

  const unmatched: string[] = [];
  for (const apiError of body.errors) {
    const control: AbstractControl | null = apiError.field ? form.get(apiError.field) : null;
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: apiError.message });
      control.markAsTouched();
    } else {
      unmatched.push(apiError.message);
    }
  }

  return unmatched.length ? unmatched.join(' ') : null;
}

/** First error message of a failed API call, for snackbars/banners. */
export function apiErrorMessage(error: unknown, fallback = 'The request failed.'): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as ApiResponse<unknown> | null;
    return body?.errors?.[0]?.message ?? body?.message ?? fallback;
  }
  return fallback;
}
