# Open Questions & Missing Business Information

Answers are recorded here and then copied into the relevant document.  
**Blocking** = must be answered before that module starts.

## Answered

| ID | Question | Answer | Date |
|---|---|---|---|
| Q-HOSP-1 | Single facility or multiple branches? | Single facility only | 2026-10-31 |
| Q-TECH-1 | .NET version | .NET 10 LTS | 2026-10-31 |
| Q-TECH-2 | Project granularity | 4 projects per module | 2026-10-31 |
| Q-TECH-3 | UI library | Angular Material | 2026-10-31 |
| Q-TECH-4 | Test projects | Not required | 2026-10-31 |
| Q-SEC-1 | Super Admin excluded from clinical data by default? | Yes — clinical permissions need an explicit, audited grant | 2026-10-31 |
| Q-SEC-2 | Password policy | Min 8 chars, upper + lower + digit; forced change on first login / admin reset; no expiry | 2026-10-31 |
| Q-SEC-3 | Session timeout | UI auto-logout after 30 min inactivity; refresh token 7 days | 2026-10-31 |
| Q-SEC-4 | Login identifier | Username or email | 2026-10-31 |
| Q-ADM-2 | Financial year | July–June (editable in Settings) | 2026-10-31 |
| Q-PAT-1 | MR number format | `MR` + YY + 6 digits, never resets (`MR26000001`); editable in Settings & Numbering | 2026-10-31 |
| Q-PAT-2 | Mandatory registration fields | Full name, gender, date of birth or age (estimated DOB), mobile | 2026-10-31 |
| Q-PAT-3 | Patients without own mobile | Guardian's mobile allowed; numbers may be shared | 2026-10-31 |
| Q-PAT-4 | Duplicate rules | Warn: same mobile + similar name, or similar name + same DOB + gender. Block: exact (normalized) ID number | 2026-10-31 |
| Q-PAT-5 | Identification types | NID, Birth registration, Passport, Other | 2026-10-31 |
| Q-PAT-6 | Address structure | Line + Upazila/Thana (free text) + District (64-district list) + 4-digit post code | 2026-10-31 |
| Q-PAT-7 | Bangla name | Optional field | 2026-10-31 |
| Q-PAT-8 | Photo | Upload only (PNG/JPEG/WebP ≤ 2 MB); webcam capture deferred | 2026-10-31 |
| Q-PAT-9 | Documents | PDF/JPEG/PNG ≤ 10 MB, type checked from content | 2026-10-31 |
| Q-PAT-10 | Merge | Receptionist requests, Hospital Administrator approves; source becomes read-only `Merged`, nothing deleted | 2026-10-31 |
| Q-PAT-11 | Registration fee | None in Phase 1 (Billing, Phase 3) | 2026-10-31 |
| Q-PAT-12 | ID card | Printable credit-card page with Code 128 barcode (browser print / save as PDF) | 2026-10-31 |
| Q-HR-1 | Employee code | Auto-generated `EMP000001` (EMPLOYEE_CODE sequence, format editable) — default used, not explicitly confirmed | 2026-10-31 |
| Q-HR-2 | Employment types | Permanent, Contract, Visiting, Intern — default used, not explicitly confirmed | 2026-10-31 |
| Q-CLIN-1 | Prescription language / layout | English on the hospital letterhead with doctor's qualifications, BMDC number and signature line; advice free text (Bangla allowed) — default used, not explicitly confirmed | 2026-10-31 |
| Q-CLIN-2 | Medicines | Structured free text: form, name, strength, dose pattern (1+0+1), before/after food, duration, route, quantity, instructions; linked to Pharmacy in Phase 6 — default used | 2026-10-31 |
| Q-CLIN-3 | Diagnosis coding | Free text, one primary + secondary; ICD-10 later — default used | 2026-10-31 |
| Q-CLIN-4 | Vitals | BP, pulse, temperature (°F), respiratory rate, SpO₂, weight, height, BMI — default used | 2026-10-31 |
| Q-CLIN-5 | Follow-up fee | Reception chooses New / Follow-up when booking (no automatic window) — default used | 2026-10-31 |
| Q-CLIN-6 | Payment before consultation | Not enforced until Billing (Phase 3) — default used | 2026-10-31 |
| Q-BILL-1 | Price list | One price per service; no corporate/insurance rates yet — default used | 2026-10-31 |
| Q-BILL-2 | OPD charge | Posted automatically at check-in from the appointment fee; paid at the cash counter; never blocks the consultation — default used | 2026-10-31 |
| Q-BILL-3 | Payment methods | Cash, card, bKash, Nagad, Rocket (reference required), bank transfer; partial payments and advance deposits — default used | 2026-10-31 |
| Q-BILL-4 | Discounts | Amount or %, reason required; above 10% of the subtotal in total needs Billing.Discount.Approve (Hospital Administrator) — default used | 2026-10-31 |
| Q-BILL-5 | Refunds | Always approved by a Hospital Administrator, then paid out by the cashier; invoice refunds return all cash and cancel the invoice (partial service refunds need credit notes, later) — default used. Enforced since 2026-11-06: nobody approves their own request, and the approver cannot pay it out | 2026-10-31 |
| Q-BILL-6 | VAT | None on medical services — default used | 2026-10-31 |
| Q-BILL-7 | Receipt | Printable A5 money receipt and A5 invoice — default used | 2026-10-31 |
| Q-BILL-8 | Who collects at reception | Billing permissions go to Accountant / Cashier by default; a hospital where reception collects grants Billing.View + Billing.Payment.Collect to the Receptionist role — default used | 2026-10-31 |
| Q-IPD-1 | Bed rent | Per calendar day, noon rule: admission day always counts; a later day counts once 12:00 noon passes in hospital; the discharge day is free if the patient leaves before noon. Admission day priced at the first bed, later days at the bed held at noon — confirmed | 2026-10-31 |
| Q-IPD-2 | Admission deposit | Suggested per bed category, shown as a warning, never blocks admission; taken as an advance deposit and applied at settlement — confirmed | 2026-10-31 |
| Q-IPD-3 | Discharge with an unpaid bill | Doctor's discharge order never blocked; release needs a settled bill or a Hospital Administrator's "discharge with due" with a reason; the due stays on the account — confirmed | 2026-10-31 |
| Q-IPD-4 | Phase 4 split | Step 1: beds, admission, transfer, bed charges, deposit, discharge; Step 2: nursing (medication orders, MAR, vitals, intake/output, notes, handover) and doctor progress notes — confirmed | 2026-10-31 |
| Q-IPD-5 | Building/floor register | Free text on the ward until a building register is needed — default used | 2026-10-31 |
| Q-IPD-6 | Changing a bed's rate | Applies to bed days not yet posted; days already on the bill keep their price — default used | 2026-10-31 |
| Q-NUR-1 | Where inpatient notes, orders and nursing records live | Clinical: progress notes, medication orders, ward vitals; new Nursing module: MAR, intake/output, nursing notes, handover, roster — confirmed | 2026-11-01 |
| Q-NUR-2 | MAR dose times | Standard ward times per frequency (OD 9am, BD 9am/9pm, TDS 8am/2pm/10pm, QID 12am/6am/12pm/6pm), interval frequencies from the start, STAT once, PRN on demand; outcomes Given / Omitted / Refused / Held (reason required) — confirmed | 2026-11-01 |
| Q-NUR-3 | Nurse–ward assignment | Soft roster: fixed shifts (Morning 8–2, Evening 2–8, Night 8–8); administrators assign nurses per ward and shift; it sets "my ward" but does not limit access; one handover per ward per shift — confirmed | 2026-11-01 |
| Q-NUR-4 | Billing medicines given on the ward | Not yet: medicine charges come with Pharmacy (Phase 6); orders stay free text like OPD prescriptions — confirmed | 2026-11-01 |
| Q-NUR-5 | Who writes inpatient notes and orders | Any doctor with `Clinical.Inpatient.Write` linked to a doctor profile (duty and on-call doctors cover wards); after discharge only treating doctors can read the chart — default used | 2026-11-01 |
| Q-NUR-6 | Recording windows | Dose up to 2 hours early, late documentation allowed; administration and fluid times within the last 24 hours; order start within the last hour or next 7 days; handover for today's or yesterday's shifts — default used | 2026-11-01 |
| Q-PAT-14 | Merging a duplicate with open work | Blocked until resolved (upcoming appointments, stay in progress, dues, draft bills, deposits, unpaid refunds); the duplicate's past history is shown on the surviving record — confirmed | 2026-11-01 |
| Q-PAT-15 | Deceased status | Set by an IPD discharge of type Death; no new appointments or admissions; record stays correctable; "Clear deceased status" corrects a mistake (audited) — default used | 2026-11-01 |
| Q-BILL-9 | Registration fee | Price-list service REG billed automatically at registration as its own invoice when active and priced (seeded at 0) — confirmed | 2026-11-01 |
| Q-BILL-10 | Inpatient admission and round fees | ADM service posted once at admission; doctor's inpatient visit fee (doctor profile) once per doctor per day on a ward-round or consultant-review note; on-call notes free — confirmed | 2026-11-01 |
| Q-BILL-11 | Configurable billing rules | Discount approval limit (default 10%) and payment methods in use (cash and advance always on) in Billing settings — default used | 2026-11-01 |
| Q-NOTIF-1 | In-app notifications | Appointment booked → doctor; patient admitted / discharged → attending doctor; discharge ordered → final-bill staff; final bill ready → cashiers; discount / refund awaiting approval → approvers; each type can be switched off — default used | 2026-11-01 |
| Q-HOSP-4 | Departments to seed | Standard list of 14 (editable); hospital can adjust in the app | 2026-10-31 |

## Hospital discovery (BRD §13) — affects module activation and phase order

| ID | Question | Blocking for |
|---|---|---|
| Q-HOSP-2 | Number of beds, wards, cabins? | IPD (P4) |
| Q-HOSP-3 | Approximate number of doctors and nurses? | Sizing only |
| Q-HOSP-4 | Which departments currently operate? (Used as seed data) | Administration (P1), non-blocking |
| Q-HOSP-5 | In-house lab? pharmacy? radiology? OT? emergency? blood bank? | Phases 5–8 |
| Q-HOSP-6 | Is integrated accounting required, or is there existing accounting software? | Finance (P9) |
| Q-HOSP-7 | SMS notifications required? Which provider? | Notifications |
| Q-HOSP-8 | Online / patient self-service booking required? | Appointment (P2) |
| Q-HOSP-9 | Separate doctor/nurse mobile app required? | Out of scope for now |

## Administration

| ID | Question | Proposed default | Blocking for |
|---|---|---|---|
| Q-ADM-1 | Who owns the hospital **service master and prices**: Administration or Billing? | Billing (see MODULE-DEPENDENCIES §3) | Billing |

## Ownership (confirm before the relevant phase)

| ID | Question | Proposed |
|---|---|---|
| Q-OWN-1 | Doctor profile owner | HR (Employee + DoctorProfile); schedules in Appointment — implemented |
| Q-OWN-2 | Bed hierarchy owner | Admin owns Building/Floor; IPD owns Ward/Room/Bed |
| Q-OWN-3 | Encounter owner | Clinical |

## UI

| ID | Question | Proposed default |
|---|---|---|
| Q-UI-1 | Bangla UI translation: needed in which phase? | English first, i18n-ready; Bangla before UAT |
| Q-UI-2 | Brand colours / logo for the theme | Neutral Material theme until provided |

## HR

| ID | Question | Current behaviour | Blocking for |
|---|---|---|---|
| Q-HR-3 | When an employee resigns or is terminated, should their linked login account be deactivated automatically? | Not automatic; an administrator deactivates the login separately. | No |
| Q-HR-4 | Should a mobile number be unique among current employees? | Not enforced. | No |

## Patient (follow-ups)

| ID | Question | Current behaviour |
|---|---|---|
| Q-PAT-13 | Webcam capture for patient photos? | Upload only |
| Q-PAT-14 | Should Pharmacist, Laboratory Technician, Radiologist and Cashier get `Patient.View` by default now, or when their modules are built? | Granted when their modules are built |
