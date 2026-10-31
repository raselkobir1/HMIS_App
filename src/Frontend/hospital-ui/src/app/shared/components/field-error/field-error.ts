import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { map, merge, of, switchMap } from 'rxjs';

/**
 * Renders the first validation message for a control (client-side or `server` errors from the API).
 * Use inside <mat-error>.
 */
@Component({
  selector: 'app-field-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `{{ message() }}`,
})
export class FieldError {
  readonly control = input.required<AbstractControl>();

  private readonly errors = toSignal(
    toObservable(this.control).pipe(
      switchMap((control) => merge(of(null), control.statusChanges).pipe(map(() => control.errors))),
    ),
    { initialValue: null },
  );

  protected readonly message = computed(() => {
    const errors = this.errors();
    if (!errors) {
      return '';
    }
    if (errors['server']) return errors['server'] as string;
    if (errors['required']) return 'This field is required.';
    if (errors['email']) return 'Enter a valid email address.';
    if (errors['minlength']) return `Must be at least ${errors['minlength'].requiredLength} characters.`;
    if (errors['maxlength']) return `Must be at most ${errors['maxlength'].requiredLength} characters.`;
    if (errors['pattern']) return 'The format is not valid.';
    if (errors['passwordPolicy']) return errors['passwordPolicy'] as string;
    if (errors['mismatch']) return 'Passwords do not match.';
    return 'The value is not valid.';
  });
}
