import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, input, OnInit, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import JsBarcode from 'jsbarcode';

import { BrandingService } from '../../../core/services/branding.service';
import { PatientApiService } from '../patient-api.service';
import { ageLabel, bloodGroupLabel, Patient } from '../patient.models';

/**
 * Printable patient ID card (credit-card size, 85.6 × 54 mm) with a Code 128 barcode of the MR number.
 * Use the browser's Print (or "Save as PDF"); everything except the card is hidden when printing.
 */
@Component({
  selector: 'app-patient-id-card-page',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toolbar no-print">
      <a mat-button [routerLink]="['/patients', id()]"><mat-icon>arrow_back</mat-icon> Back to patient</a>
      <button mat-flat-button type="button" (click)="print()" [disabled]="!patient()"><mat-icon>print</mat-icon> Print</button>
    </div>

    @if (patient(); as p) {
      <div class="card print-area">
        <header>
          @if (branding.logoUrl(); as logo) {
            <img [src]="logo" alt="" class="logo" />
          }
          <div class="hospital">{{ branding.name() }}</div>
        </header>
        <div class="body">
          <div class="name">{{ p.fullName }}</div>
          <div class="line">{{ age() }} · {{ p.gender }} · Blood group {{ bloodGroup() }}</div>
          <div class="line">Mobile {{ p.mobileNumber }}</div>
        </div>
        <svg #barcode class="barcode" role="img" [attr.aria-label]="'Barcode ' + p.mrNumber"></svg>
      </div>
    }
  `,
  styles: `
    .toolbar {
      display: flex;
      justify-content: space-between;
      margin-bottom: 16px;
    }
    .card {
      width: 85.6mm;
      height: 54mm;
      box-sizing: border-box;
      padding: 3mm 4mm;
      border: 1px solid #bbb;
      border-radius: 3mm;
      background: #fff;
      color: #111;
      display: flex;
      flex-direction: column;
      font-family: Roboto, Arial, sans-serif;
    }
    header {
      display: flex;
      align-items: center;
      gap: 2mm;
      border-bottom: 0.3mm solid #0b6e6e;
      padding-bottom: 1mm;
    }
    .logo {
      height: 7mm;
      width: 7mm;
      object-fit: contain;
    }
    .hospital {
      font-size: 3.4mm;
      font-weight: 600;
      color: #0b6e6e;
    }
    .body {
      flex: 1;
      padding-top: 1.5mm;
    }
    .name {
      font-size: 3.8mm;
      font-weight: 600;
    }
    .line {
      font-size: 2.8mm;
    }
    .barcode {
      height: 13mm;
      width: 100%;
    }
  `,
})
export class PatientIdCardPage implements OnInit {
  readonly id = input.required<string>();

  protected readonly branding = inject(BrandingService);
  private readonly api = inject(PatientApiService);
  private readonly barcode = viewChild<ElementRef<SVGElement>>('barcode');

  protected readonly patient = signal<Patient | null>(null);
  protected readonly age = computed(() => {
    const p = this.patient();
    return p ? ageLabel(p.dateOfBirth, p.isDateOfBirthEstimated) : '';
  });
  protected readonly bloodGroup = computed(() => bloodGroupLabel(this.patient()?.bloodGroup));

  constructor() {
    effect(() => {
      const svg = this.barcode()?.nativeElement;
      const p = this.patient();
      if (svg && p) {
        JsBarcode(svg, p.mrNumber, { format: 'CODE128', displayValue: true, fontSize: 14, height: 40, margin: 0 });
      }
    });
  }

  ngOnInit(): void {
    this.api.getById(this.id()).subscribe((p) => this.patient.set(p));
  }

  protected print(): void {
    window.print();
  }
}
