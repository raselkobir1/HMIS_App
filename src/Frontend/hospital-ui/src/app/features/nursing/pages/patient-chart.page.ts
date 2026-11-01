import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { ChartDoctorTab, ChartVitalsTab } from '../components/doctor-tabs';
import { ChartFluidsTab, ChartMarTab, ChartNursingNotesTab } from '../components/nursing-tabs';
import { ageOf, Allergy, InpatientChart, NursingApiService, NursingPatient, NursingPermissions } from '../nursing.models';

interface Header {
  admissionId: string;
  admissionNumber: string;
  status: string;
  isActive: boolean;
  patientId: string;
  name: string | null;
  mrNumber: string | null;
  gender: string | null;
  dateOfBirth: string | null;
  wardName: string | null;
  bedNumber: string | null;
  doctorName: string | null;
  admittedAt: string;
  allergies: Allergy[];
}

/**
 * The inpatient chart: doctor's orders and notes, MAR, vitals, intake/output and nursing notes in tabs.
 * Tabs follow permissions (Clinical.View for the doctor's side and vitals, Nursing.View for nursing records).
 * Route: /nursing/patients/:id (the admission id).
 */
@Component({
  selector: 'app-patient-chart-page',
  imports: [DatePipe, RouterLink, MatTabsModule, MatButtonModule, MatIconModule, ChartDoctorTab, ChartVitalsTab, ChartMarTab, ChartFluidsTab, ChartNursingNotesTab],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <div class="banner error" role="alert">{{ message }}</div>
    } @else if (header(); as h) {
      <header class="head">
        <div>
          <a mat-button [routerLink]="backLink" class="back"><mat-icon>arrow_back</mat-icon> {{ backLabel }}</a>
          <h1>
            {{ h.name }}
            <span class="meta">{{ h.gender }}{{ h.dateOfBirth ? ', ' + age(h.dateOfBirth) : '' }}</span>
            @if (!h.isActive) {
              <span class="badge">Discharged</span>
            }
          </h1>
          <div class="sub">
            {{ h.mrNumber }} · {{ h.admissionNumber }} · {{ h.wardName }} bed {{ h.bedNumber }} · {{ h.doctorName }} ·
            admitted {{ h.admittedAt | date: 'd MMM, h:mm a' }}
          </div>
        </div>
        @if (can.ipd) {
          <a mat-stroked-button [routerLink]="['/ipd/admissions', h.admissionId]"><mat-icon>hotel</mat-icon> Admission</a>
        }
      </header>
      @if (h.allergies.length) {
        <div class="banner allergy" role="alert">
          <mat-icon>warning</mat-icon>
          <span><strong>Allergies:</strong> {{ allergyText() }}</span>
        </div>
      } @else {
        <div class="no-allergy secondary">No known allergies recorded.</div>
      }
      @if (!h.isActive) {
        <div class="banner info" role="status"><mat-icon>lock</mat-icon> The patient has been discharged; the chart is read-only.</div>
      }

      <mat-tab-group [(selectedIndex)]="tab" mat-stretch-tabs="false" animationDuration="0">
        @if (can.nursing) {
          <mat-tab label="MAR">
            <ng-template matTabContent><app-chart-mar-tab [admissionId]="h.admissionId" /></ng-template>
          </mat-tab>
        }
        @if (chart(); as c) {
          <mat-tab label="Orders & notes">
            <ng-template matTabContent><app-chart-doctor-tab [chart]="c" [patientLabel]="label()" (changed)="reloadChart()" /></ng-template>
          </mat-tab>
          <mat-tab label="Vitals">
            <ng-template matTabContent><app-chart-vitals-tab [chart]="c" [patientLabel]="label()" (changed)="reloadChart()" /></ng-template>
          </mat-tab>
        }
        @if (can.nursing) {
          <mat-tab label="Intake / output">
            <ng-template matTabContent><app-chart-fluids-tab [admissionId]="h.admissionId" /></ng-template>
          </mat-tab>
          <mat-tab label="Nursing notes">
            <ng-template matTabContent><app-chart-nursing-notes-tab [admissionId]="h.admissionId" /></ng-template>
          </mat-tab>
        }
      </mat-tab-group>
    } @else {
      <p class="secondary">Loading…</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; }
    .back { margin-left: -12px; }
    h1 { margin: 4px 0; font: var(--mat-sys-headline-small); display: flex; flex-wrap: wrap; align-items: baseline; gap: 10px; }
    .meta { font: var(--mat-sys-body-large); color: var(--mat-sys-on-surface-variant); }
    .sub { color: var(--mat-sys-on-surface-variant); }
    .badge { padding: 2px 10px; border-radius: 999px; font: var(--mat-sys-label-medium); background: var(--mat-sys-surface-container-highest); }
    .banner { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-radius: 8px; }
    .banner.error, .banner.allergy { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .banner.info { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .secondary { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class PatientChartPage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(NursingApiService);
  private readonly auth = inject(AuthService);

  protected readonly can = {
    nursing: this.auth.hasPermission(NursingPermissions.view),
    clinical: this.auth.hasPermission(NursingPermissions.clinicalView),
    ipd: this.auth.hasPermission('Ipd.View'),
  };
  protected readonly backLink = this.can.nursing ? '/nursing/ward' : '/ipd/admissions';
  protected readonly backLabel = this.can.nursing ? 'Ward' : 'Admissions';
  protected readonly chart = signal<InpatientChart | null>(null);
  private readonly nursingPatient = signal<NursingPatient | null>(null);
  protected readonly error = signal<string | null>(null);
  /** Doctors start on their orders and notes, nurses on the MAR. */
  protected tab = 0;

  protected readonly header = computed<Header | null>(() => {
    const p = this.nursingPatient();
    if (p) {
      return {
        admissionId: p.admissionId, admissionNumber: p.admissionNumber, status: p.status, isActive: p.isActive, patientId: p.patientId,
        name: p.patientName, mrNumber: p.mrNumber, gender: p.gender, dateOfBirth: p.dateOfBirth, wardName: p.wardName,
        bedNumber: p.bedNumber, doctorName: p.attendingDoctorName, admittedAt: p.admittedAt, allergies: p.allergies,
      };
    }
    const c = this.chart();
    return c
      ? {
          admissionId: c.stay.admissionId, admissionNumber: c.stay.admissionNumber, status: c.stay.status, isActive: c.stay.isActive,
          patientId: c.patient.id, name: c.patient.fullName, mrNumber: c.patient.mrNumber, gender: c.patient.gender,
          dateOfBirth: c.patient.dateOfBirth, wardName: c.stay.wardName, bedNumber: c.stay.bedNumber, doctorName: c.stay.attendingDoctorName,
          admittedAt: c.stay.admittedAt, allergies: c.allergies,
        }
      : null;
  });
  protected readonly label = computed(() => {
    const h = this.header();
    return h ? `${h.name} · ${h.mrNumber} · ${h.wardName} bed ${h.bedNumber}` : '';
  });
  protected readonly allergyText = computed(() =>
    (this.header()?.allergies ?? []).map((a) => `${a.allergen} (${a.severity.toLowerCase()}${a.reaction ? ', ' + a.reaction : ''})`).join(' · '),
  );
  protected readonly age = ageOf;

  ngOnInit(): void {
    if (this.can.nursing) {
      this.api.patient(this.id()).subscribe({
        next: (p) => this.nursingPatient.set(p),
        error: (err) => this.error.set(apiErrorMessage(err, 'The patient could not be loaded.')),
      });
    }
    if (this.can.clinical) {
      this.reloadChart();
    }
  }

  protected reloadChart(): void {
    this.api.chart(this.id()).subscribe({
      next: (c) => {
        if (!this.chart() && c.isDoctor && this.can.nursing) {
          this.tab = 1;
        }
        this.chart.set(c);
      },
      error: (err) => {
        if (!this.can.nursing) {
          this.error.set(apiErrorMessage(err, 'The chart could not be loaded.'));
        }
      },
    });
  }
}
