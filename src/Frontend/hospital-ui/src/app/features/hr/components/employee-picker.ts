import { ChangeDetectionStrategy, Component, effect, inject, input, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, distinctUntilChanged, filter, of, switchMap } from 'rxjs';

import { HrApiService } from '../hr-api.service';
import { EmployeeLookupItem } from '../hr.models';

/** Searchable picker for current employees; two-way binds the selected employee id. */
@Component({
  selector: 'app-employee-picker',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-form-field class="full">
      <mat-label>{{ label() }}</mat-label>
      <input matInput [formControl]="search" [matAutocomplete]="auto" placeholder="Type a name or employee code" />
      @if (employeeId()) {
        <button mat-icon-button matSuffix type="button" aria-label="Clear employee" (click)="clear()">
          <mat-icon>close</mat-icon>
        </button>
      }
      <mat-hint>{{ hint() }}</mat-hint>
      <mat-autocomplete #auto="matAutocomplete" (optionSelected)="select($event)">
        @for (e of options(); track e.id) {
          <mat-option [value]="e">{{ e.employeeCode }} · {{ e.fullName }}{{ e.designationName ? ' (' + e.designationName + ')' : '' }}</mat-option>
        }
      </mat-autocomplete>
    </mat-form-field>
  `,
  styles: `
    .full {
      width: 100%;
    }
  `,
})
export class EmployeePicker {
  private readonly api = inject(HrApiService);

  readonly employeeId = model<string | null>(null);
  /** Text shown for an already-selected employee (e.g. "EMP000001 · Dr. Karim"). */
  readonly initialLabel = input<string | null>(null);
  readonly label = input('Linked employee');
  readonly hint = input('Optional — connects this login to an HR employee record');

  protected readonly search = new FormControl<string | EmployeeLookupItem>('', { nonNullable: true });
  protected readonly options = signal<EmployeeLookupItem[]>([]);

  constructor() {
    // The label of an already-linked employee may arrive after the dialog opens.
    effect(() => this.search.setValue(this.initialLabel() ?? '', { emitEvent: false }));

    this.search.valueChanges
      .pipe(
        filter((value): value is string => typeof value === 'string'),
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((term) => (term.trim().length < 2 ? of([]) : this.api.lookup(term.trim()).pipe(catchError(() => of([]))))),
        takeUntilDestroyed(),
      )
      .subscribe((items) => this.options.set(items));
  }

  protected select(event: MatAutocompleteSelectedEvent): void {
    const employee = event.option.value as EmployeeLookupItem;
    this.employeeId.set(employee.id);
    this.search.setValue(`${employee.employeeCode} · ${employee.fullName}`, { emitEvent: false });
  }

  protected clear(): void {
    this.employeeId.set(null);
    this.search.setValue('', { emitEvent: false });
    this.options.set([]);
  }
}
