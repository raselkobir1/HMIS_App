import { AbstractControl, ValidationErrors } from '@angular/forms';

/**
 * Mirrors the server password policy (Q-SEC-2): 8–128 characters with upper, lower and a digit.
 * The API remains the authority; this only gives immediate feedback.
 */
export function passwordPolicyValidator(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value ?? '';
  if (!value) return { required: true };
  if (value.length < 8) return { passwordPolicy: 'Use at least 8 characters.' };
  if (value.length > 128) return { passwordPolicy: 'Use at most 128 characters.' };
  if (!/[A-Z]/.test(value)) return { passwordPolicy: 'Include at least one uppercase letter.' };
  if (!/[a-z]/.test(value)) return { passwordPolicy: 'Include at least one lowercase letter.' };
  if (!/[0-9]/.test(value)) return { passwordPolicy: 'Include at least one digit.' };
  return null;
}
