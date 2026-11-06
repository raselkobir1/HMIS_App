import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, forkJoin, map, Observable, of, startWith, switchMap, tap } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { PatientPicker } from '../../patient/components/patient-picker';
import { PatientApiService } from '../../patient/patient-api.service';
import { PatientListItem } from '../../patient/patient.models';
import { BillableService, BillingApiService, Invoice, InvoiceLineInput, ServiceCategory } from '../billing.models';
import { TakaPipe } from '../components/taka.pipe';

interface EditLine {
  key: string;
  /** Posted by the ward (e.g. bed rent): shown, never edited or sent back. */
  locked: boolean;
  id: string | null;
  serviceId: string;
  code: string;
  name: string;
  category: ServiceCategory;
  isPriceEditable: boolean;
  quantity: number;
  unitPrice: number;
}

/**
 * Creates or edits a draft invoice from the price list. Routes: /billing/invoices/new (optional ?patientId=) and
 * /billing/invoices/:id/edit. Finalizing locks the invoice for payment.
 */
@Component({
  selector: 'app-invoice-editor-page',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatIconModule,
    PageHeader,
    PatientPicker,
    TakaPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="draft() ? 'Edit ' + draft()!.invoiceNumber : 'New invoice'" subtitle="Draft invoices can be changed until they are finalized">
      @if (draft(); as d) {
        <a mat-button [routerLink]="['/billing/invoices', d.id]"><mat-icon>arrow_back</mat-icon> Back to invoice</a>
      }
    </app-page-header>

    @if (loadError(); as message) {
      <div class="banner" role="alert">{{ message }}</div>
    } @else {
      @if (error(); as message) {
        <div class="banner" role="alert">{{ message }}</div>
      }
      <mat-card appearance="outlined">
        <mat-card-content>
          @if (draft(); as d) {
            <p class="patient"><strong>{{ d.patient?.fullName }}</strong> · {{ d.patient?.mrNumber }}</p>
          } @else {
            <app-patient-picker [(patient)]="patient" />
          }

          <mat-form-field class="full">
            <mat-label>Add a service</mat-label>
            <mat-icon matPrefix>add_shopping_cart</mat-icon>
            <input matInput [formControl]="serviceSearch" [matAutocomplete]="auto" placeholder="Code or name" />
            <mat-autocomplete #auto="matAutocomplete" (optionSelected)="addService($event)">
              @for (s of serviceOptions(); track s.id) {
                <mat-option [value]="s">
                  <strong>{{ s.code }}</strong> · {{ s.name }} · {{ s.isPriceEditable ? 'price at billing' : (s.price | taka) }}
                </mat-option>
              } @empty {
                <mat-option disabled>No matching active service</mat-option>
              }
            </mat-autocomplete>
          </mat-form-field>

          <div class="lines" role="table" aria-label="Invoice lines">
            <div class="line head" role="row">
              <span role="columnheader">Service</span>
              <span role="columnheader" class="num">Qty</span>
              <span role="columnheader" class="num">Unit price</span>
              <span role="columnheader" class="num">Total</span>
              <span role="columnheader"><span class="cdk-visually-hidden">Remove</span></span>
            </div>
            @for (l of lines(); track l.key; let i = $index) {
              <div class="line" role="row">
                <span role="cell">
                  {{ l.name }}
                  <span class="secondary">{{ l.locked ? 'Posted by the ward' : l.code + ' · ' + l.category }}</span>
                </span>
                <span role="cell" class="num">
                  @if (l.locked) {
                    {{ l.quantity }}
                  } @else {
                    <input class="cell-input" type="number" min="1" max="1000" [ngModel]="l.quantity"
                           (ngModelChange)="setQuantity(i, $event)" [attr.aria-label]="'Quantity of ' + l.name" />
                  }
                </span>
                <span role="cell" class="num">
                  @if (l.isPriceEditable && !l.locked) {
                    <input class="cell-input price" type="number" min="0" step="0.01" [ngModel]="l.unitPrice"
                           (ngModelChange)="setPrice(i, $event)" [attr.aria-label]="'Price of ' + l.name" />
                  } @else {
                    {{ l.unitPrice | taka }}
                  }
                </span>
                <span role="cell" class="num">{{ l.quantity * l.unitPrice | taka }}</span>
                <span role="cell">
                  @if (!l.locked) {
                    <button mat-icon-button type="button" (click)="remove(i)" [attr.aria-label]="'Remove ' + l.name"><mat-icon>delete</mat-icon></button>
                  }
                </span>
              </div>
            } @empty {
              <p class="secondary empty">Add services from the price list above.</p>
            }
            <div class="line total" role="row">
              <span role="cell">Subtotal</span>
              <span role="cell"></span>
              <span role="cell"></span>
              <span role="cell" class="num">{{ subtotal() | taka }}</span>
              <span role="cell"></span>
            </div>
          </div>

          <mat-form-field class="full notes">
            <mat-label>Notes (optional)</mat-label>
            <input matInput [(ngModel)]="notes" maxlength="500" />
          </mat-form-field>
        </mat-card-content>
        <mat-card-actions align="end">
          <button mat-stroked-button type="button" [disabled]="!canSave()" (click)="save(false)">Save draft</button>
          @if (!draft()?.isRunningBill || draft()?.isSupplementary) {
            <button mat-flat-button type="button" [disabled]="!canSave() || subtotal() <= 0" (click)="save(true)">
              <mat-icon>lock</mat-icon> Save & finalize
            </button>
          }
        </mat-card-actions>
      </mat-card>
    }
  `,
  styles: `
    .banner { padding: 12px; margin-bottom: 12px; border-radius: 8px; background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .full { width: 100%; }
    .patient { margin: 0 0 12px; }
    .secondary { display: block; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .lines { margin-bottom: 12px; }
    .line {
      display: grid; grid-template-columns: minmax(0, 1fr) 80px 120px 120px 48px; gap: 8px; align-items: center;
      padding: 6px 0; border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .line.head { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
    .line.total { font-weight: 600; border-bottom: 0; }
    .num { text-align: right; }
    .cell-input {
      width: 64px; padding: 6px; text-align: right; font: inherit; color: inherit; background: transparent;
      border: 1px solid var(--mat-sys-outline); border-radius: 4px;
    }
    .cell-input.price { width: 100px; }
    .empty { padding: 12px 0; }
    @media (max-width: 600px) {
      .line { grid-template-columns: minmax(0, 1fr) 64px 96px 40px; }
      .line > :nth-child(3) { display: none; }
    }
  `,
})
export class InvoiceEditorPage implements OnInit {
  /** Route parameter when editing a draft. */
  readonly id = input<string>();
  /** Query parameter when starting from a patient account. */
  readonly patientId = input<string>();

  private readonly api = inject(BillingApiService);
  private readonly patientApi = inject(PatientApiService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly picker = viewChild(PatientPicker);

  protected readonly draft = signal<Invoice | null>(null);
  protected readonly patient = signal<PatientListItem | null>(null);
  protected readonly lines = signal<EditLine[]>([]);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected notes = '';

  protected readonly serviceSearch = new FormControl<string | BillableService>('', { nonNullable: true });
  protected readonly serviceOptions = signal<BillableService[]>([]);
  protected readonly subtotal = computed(() => this.lines().reduce((sum, l) => sum + Math.round(l.quantity * l.unitPrice * 100) / 100, 0));
  protected readonly canSave = computed(
    () =>
      !this.saving() &&
      (!!this.draft() || !!this.patient()) &&
      this.lines().every((l) => Number.isInteger(l.quantity) && l.quantity >= 1 && l.quantity <= 1000 && l.unitPrice >= 0),
  );

  constructor() {
    this.serviceSearch.valueChanges
      .pipe(
        startWith(''),
        map((v) => (typeof v === 'string' ? v.trim() : null)),
        debounceTime(250),
        switchMap((term) =>
          term === null
            ? of(this.serviceOptions())
            : this.api.services({ pageNumber: 1, pageSize: 20, search: term || undefined, isActive: true }).pipe(
                map((page) => page.items),
                catchError(() => of([])),
              ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((items) => this.serviceOptions.set(items));
  }

  ngOnInit(): void {
    const id = this.id();
    if (id) {
      this.api.invoice(id).subscribe({
        next: (invoice) => this.loadDraft(invoice),
        error: (err) => this.loadError.set(apiErrorMessage(err, 'The invoice could not be loaded.')),
      });
      return;
    }
    const patientId = this.patientId();
    if (patientId) {
      this.patientApi.getById(patientId).subscribe((p) =>
        this.picker()?.setPatient({
          id: p.id,
          mrNumber: p.mrNumber,
          fullName: p.fullName,
          gender: p.gender,
          dateOfBirth: p.dateOfBirth,
          isDateOfBirthEstimated: p.isDateOfBirthEstimated,
          mobileNumber: p.mobileNumber,
          district: null,
          status: p.status,
        }),
      );
    }
  }

  protected addService(event: MatAutocompleteSelectedEvent): void {
    const s = event.option.value as BillableService;
    this.lines.update((lines) => [
      ...lines,
      {
        key: crypto.randomUUID(),
        locked: false,
        id: null,
        serviceId: s.id,
        code: s.code,
        name: s.name,
        category: s.category,
        isPriceEditable: s.isPriceEditable,
        quantity: 1,
        unitPrice: s.price,
      },
    ]);
    this.serviceSearch.setValue('');
  }

  protected setQuantity(index: number, value: number | null): void {
    this.lines.update((lines) => lines.map((l, i) => (i === index ? { ...l, quantity: Number(value ?? 0) } : l)));
  }

  protected setPrice(index: number, value: number | null): void {
    this.lines.update((lines) => lines.map((l, i) => (i === index ? { ...l, unitPrice: Number(value ?? 0) } : l)));
  }

  protected remove(index: number): void {
    this.lines.update((lines) => lines.filter((_, i) => i !== index));
  }

  protected save(finalize: boolean): void {
    const lines: InvoiceLineInput[] = this.lines().filter((l) => !l.locked).map((l) => ({
      id: l.id,
      serviceId: l.serviceId,
      quantity: l.quantity,
      unitPrice: l.isPriceEditable ? l.unitPrice : null,
    }));
    const notes = this.notes.trim() || null;
    const draft = this.draft();
    const saved$: Observable<Invoice> = draft
      ? this.api.updateInvoice(draft.id, notes, lines, draft.rowVersion)
      : this.api.createInvoice(this.patient()!.id, notes, lines);

    this.saving.set(true);
    this.error.set(null);
    saved$
      .pipe(
        // Keep the saved draft, so a retry after a failed finalize updates it instead of creating another one.
        tap((invoice) => this.draft.set(invoice)),
        switchMap((invoice) => (finalize ? this.api.finalizeInvoice(invoice.id, invoice.rowVersion) : of(invoice))),
      )
      .subscribe({
      next: (invoice) => {
        this.notifications.success(finalize ? `Invoice ${invoice.invoiceNumber} finalized.` : `Draft ${invoice.invoiceNumber} saved.`);
        void this.router.navigate(['/billing/invoices', invoice.id]);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }

  /** Existing lines keep their id; whether the price may change comes from the price list. */
  private loadDraft(invoice: Invoice): void {
    if (invoice.status !== 'Draft') {
      this.loadError.set(`${invoice.invoiceNumber} is ${invoice.status.toLowerCase()} and can no longer be edited.`);
      return;
    }
    this.draft.set(invoice);
    this.notes = invoice.notes ?? '';
    const codes = [...new Set(invoice.lines.map((l) => l.serviceCode).filter((c): c is string => !!c))];
    const lookups = codes.map((code) =>
      this.api.services({ pageNumber: 1, pageSize: 5, search: code }).pipe(
        map((page) => page.items.find((s) => s.code === code) ?? null),
        catchError(() => of(null)),
      ),
    );
    (lookups.length ? forkJoin(lookups) : of([])).subscribe((services) => {
      const editable = new Set(services.filter((s): s is BillableService => !!s?.isPriceEditable).map((s) => s.id));
      this.lines.set(
        invoice.lines.map((l) => ({
          key: l.id,
          locked: l.isPosted,
          id: l.id,
          serviceId: l.serviceId ?? '',
          code: l.serviceCode ?? '',
          name: l.description,
          category: l.category,
          isPriceEditable: !!l.serviceId && editable.has(l.serviceId),
          quantity: l.quantity,
          unitPrice: l.unitPrice,
        })),
      );
    });
  }
}
