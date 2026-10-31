import { ChangeDetectionStrategy, Component, inject, input, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, switchMap } from 'rxjs';

import { PatientApiService } from '../patient-api.service';
import { ageLabel, PatientListItem } from '../patient.models';

/** Finds an active patient by MR number, mobile, name or ID number; two-way binds the selected patient. */
@Component({
  selector: 'app-patient-picker',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-form-field class="full">
      <mat-label>{{ label() }}</mat-label>
      <mat-icon matPrefix>person_search</mat-icon>
      <input matInput [formControl]="search" [matAutocomplete]="auto" placeholder="MR number, mobile or name" />
      @if (patient()) {
        <button mat-icon-button matSuffix type="button" aria-label="Clear patient" (click)="clear()"><mat-icon>close</mat-icon></button>
      }
      <mat-autocomplete #auto="matAutocomplete" (optionSelected)="select($event)">
        @for (p of options(); track p.id) {
          <mat-option [value]="p">
            <strong>{{ p.mrNumber }}</strong> · {{ p.fullName }} · {{ age(p) }}/{{ p.gender.charAt(0) }} · {{ p.mobileNumber }}
          </mat-option>
        }
      </mat-autocomplete>
    </mat-form-field>
  `,
  styles: `.full { width: 100%; }`,
})
export class PatientPicker {
  private readonly api = inject(PatientApiService);

  readonly patient = model<PatientListItem | null>(null);
  readonly label = input('Patient');

  protected readonly search = new FormControl<string | PatientListItem>('', { nonNullable: true });
  protected readonly options = signal<PatientListItem[]>([]);

  constructor() {
    this.search.valueChanges
      .pipe(
        filter((v): v is string => typeof v === 'string'),
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((term) =>
          term.trim().length < 3
            ? of([])
            : this.api.search({ pageNumber: 1, pageSize: 10, search: term.trim(), status: 'Active' }).pipe(
                map((page) => page.items),
                catchError(() => of([])),
              ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((items) => this.options.set(items));
  }

  /** Pre-selects a patient (e.g. when opening booking from a patient profile). */
  setPatient(patient: PatientListItem): void {
    this.patient.set(patient);
    this.search.setValue(this.labelFor(patient), { emitEvent: false });
  }

  protected age(p: PatientListItem): string {
    return ageLabel(p.dateOfBirth, p.isDateOfBirthEstimated);
  }

  protected select(event: MatAutocompleteSelectedEvent): void {
    this.setPatient(event.option.value as PatientListItem);
  }

  protected clear(): void {
    this.patient.set(null);
    this.search.setValue('', { emitEvent: false });
    this.options.set([]);
  }

  private labelFor(p: PatientListItem): string {
    return `${p.mrNumber} · ${p.fullName}`;
  }
}
