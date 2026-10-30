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

## Identity / Security

| ID | Question | Proposed default | Blocking for |
|---|---|---|---|
| Q-SEC-1 | Should Super Administrator be **excluded** from clinical data (`Patient.MedicalInfo.*`)? | Yes, excluded. Super Admin can grant itself access, and that grant is audited. | Identity |
| Q-SEC-2 | Password policy | Minimum 8 characters, upper + lower + digit; change required on first login; no expiry | Identity |
| Q-SEC-3 | Idle session timeout | 30 min of inactivity on the UI; refresh token lasts 7 days | Identity |
| Q-SEC-4 | Login with username, email, or both? | Both | Identity |

## Administration

| ID | Question | Proposed default | Blocking for |
|---|---|---|---|
| Q-ADM-1 | Who owns the hospital **service master and prices**: Administration or Billing? | Billing (see MODULE-DEPENDENCIES §3) | Billing |
| Q-ADM-2 | Financial year | July–June (Bangladesh fiscal year) | Administration |

## Patient (reference module) — **blocking**

| ID | Question | Proposed default |
|---|---|---|
| Q-PAT-1 | **MR Number format** | `MR` + `YY` + 6-digit sequence, for example `MR26000123`. The sequence never resets. |
| Q-PAT-2 | **Mandatory fields** at registration | Full name, gender, and either DOB or age (age gives an estimated DOB), mobile number. Everything else is optional. |
| Q-PAT-3 | Patients without their own mobile (children, elderly) | Allow the guardian's mobile. A mobile number may be shared by several patients. |
| Q-PAT-4 | **Duplicate detection criteria** | Warn (not block) when the same mobile + similar name, **or** the same ID number, **or** a similar name + same DOB + same gender. Block only on an exact ID-number match. |
| Q-PAT-5 | Identification types | NID, Birth Registration, Passport, Other |
| Q-PAT-6 | Address structure | Free-text line + District + Upazila/Thana + Post code. Should Division/District/Upazila be **dropdowns from a BD master list**? |
| Q-PAT-7 | Bangla name field needed? | Optional field |
| Q-PAT-8 | Patient photo: webcam capture, file upload, or both? | Both, optional |
| Q-PAT-9 | Document types and size limit | PDF/JPG/PNG, maximum 10 MB |
| Q-PAT-10 | Merge: who approves, and what happens to the source record? | Hospital Admin approves. The source is marked `Merged` (read-only) and points to the target. Nothing is deleted. |
| Q-PAT-11 | Is a **registration fee** charged? | If yes, it is charged through Billing in Phase 3. In Phase 1 the receipt has no fee. |
| Q-PAT-12 | ID card format | A5/credit-card PDF with hospital logo, MR number barcode, name, age/sex and blood group |

## Ownership (confirm before the relevant phase)

| ID | Question | Proposed |
|---|---|---|
| Q-OWN-1 | Doctor profile owner | HR (Employee + DoctorProfile); schedules in Appointment |
| Q-OWN-2 | Bed hierarchy owner | Admin owns Building/Floor; IPD owns Ward/Room/Bed |
| Q-OWN-3 | Encounter owner | Clinical |

## UI

| ID | Question | Proposed default |
|---|---|---|
| Q-UI-1 | Bangla UI translation: needed in which phase? | English first, i18n-ready; Bangla before UAT |
| Q-UI-2 | Brand colours / logo for the theme | Neutral Material theme until provided |
