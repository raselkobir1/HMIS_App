# API Design Standards

Implements TAD §6.3, §6.4 and §12. Endpoints for each module are added to this file when the module is built.

## Conventions
- Base path: `/api/v1/{resource}`. Resources are plural and kebab-case, for example `/api/v1/number-sequences`.
- Versioning: URL segment `v1` (`Asp.Versioning.Mvc`).
- JSON: camelCase. Enums are strings. Dates use ISO-8601. `DateOnly` values are sent as `yyyy-MM-dd`.
- Every endpoint accepts a `CancellationToken` and declares `[HasPermission(...)]`, unless it is `[AllowAnonymous]` (login and refresh only).
- Lists: `GET /resource?pageNumber=1&pageSize=20&search=...&sortBy=name&sortDir=asc` returns `ApiResponse<PagedResponse<T>>`. `pageSize` must be 100 or less.
- Status changes use sub-resources rather than generic PUT: `PATCH /patients/{id}/status`, `POST /patients/{id}/merge-requests`.
- Concurrency: mutable resources return `rowVersion` (xmin). Updates must send it back, and a mismatch returns **409**.
- Idempotency (from Phase 3): payment and dispensing POSTs require an `Idempotency-Key` header.

## Response envelope

```json
{
  "success": false,
  "message": "Validation failed.",
  "data": null,
  "errors": [{ "code": "VALIDATION_ERROR", "message": "Mobile number is required.", "field": "mobileNumber" }],
  "traceId": "00-4bf92f...-01"
}
```

## Status codes
See ARCHITECTURE.md §6.2. Responses are never 200 with `success: false`.

## Endpoint catalogue
*(Filled in per module.)*

### Identity (implemented)
| Method | Route | Permission |
|---|---|---|
| POST | `/api/v1/auth/login` | anonymous |
| POST | `/api/v1/auth/refresh` | anonymous (refresh token from HttpOnly cookie) |
| POST | `/api/v1/auth/logout` | anonymous (revokes the session in the refresh cookie) |
| POST | `/api/v1/auth/change-password` | authenticated |
| GET | `/api/v1/auth/me` | authenticated (profile + effective permissions) |
| GET/POST | `/api/v1/users` | Identity.Users.View / Manage |
| GET/PUT | `/api/v1/users/{id}` | Identity.Users.View / Manage |
| PATCH | `/api/v1/users/{id}/status` | Identity.Users.Manage |
| POST | `/api/v1/users/{id}/reset-password` | Identity.Users.Manage |
| POST | `/api/v1/users/{id}/unlock` | Identity.Users.Manage |
| PUT | `/api/v1/users/{id}/roles` | Identity.Roles.Manage |
| PUT | `/api/v1/users/{id}/permissions` | Identity.Roles.Manage (direct grants) |
| GET/POST | `/api/v1/roles` | Identity.Roles.View / Manage |
| GET/PUT/DELETE | `/api/v1/roles/{id}` | Identity.Roles.View / Manage |
| PUT | `/api/v1/roles/{id}/permissions` | Identity.Roles.Manage |
| GET | `/api/v1/permissions` | Identity.Roles.View |
| GET | `/api/v1/login-history` | Identity.LoginHistory.View |

**Identity notes**
- Login and change-password responses include `accessToken`, `accessTokenExpiresAt` and `user` (with roles and effective permissions). The refresh token is never in a response body.
- `POST /auth/login` and `POST /auth/change-password` are rate limited per IP (`RateLimiting:AuthPermitPerMinute`, 10 by default).
- Users with `mustChangePassword = true` have **no** effective permissions until they change their password.
- Error codes: `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `ACCOUNT_INACTIVE`, `INVALID_REFRESH_TOKEN` (401); `USERNAME_TAKEN`, `EMAIL_TAKEN`, `ROLE_NAME_TAKEN`, `ROLE_IN_USE`, `CONCURRENCY_CONFLICT` (409); `INVALID_CURRENT_PASSWORD`, `PASSWORD_SAME_AS_CURRENT`, `CANNOT_CHANGE_OWN_ACCOUNT`, `LAST_SUPER_ADMINISTRATOR`, `SUPER_ADMINISTRATOR_REQUIRED`, `SUPER_ADMINISTRATOR_PERMISSIONS_REQUIRED`, `ROLE_SYSTEM_DELETE`, `ROLE_NOT_FOUND`, `PERMISSION_NOT_FOUND` (422).

### Audit (implemented)
| Method | Route | Permission |
|---|---|---|
| GET | `/api/v1/audit-logs?fromUtc&toUtc&module&entityName&entityId&action&userId&search&pageNumber&pageSize` | Audit.View |
| GET | `/api/v1/audit-logs/{id}` (includes old and new values) | Audit.View |
| GET | `/api/v1/audit-logs/filter-options` | Audit.View |

The audit log is read-only through the API. Results are always sorted by most recent first.

### Administration (implemented)
| Method | Route | Permission |
|---|---|---|
| GET | `/api/v1/hospital-profile` | Admin.HospitalProfile.View |
| PUT | `/api/v1/hospital-profile` | Admin.HospitalProfile.Manage |
| GET | `/api/v1/hospital-profile/branding` | anonymous (name, short name, logo flag) |
| GET | `/api/v1/hospital-profile/logo` | anonymous (image) |
| POST / DELETE | `/api/v1/hospital-profile/logo` (multipart `file`, PNG/JPEG/WebP ≤ 1 MB, checked by content) | Admin.HospitalProfile.Manage |
| GET | `/api/v1/system-settings` | Admin.HospitalProfile.View |
| PUT | `/api/v1/system-settings` | Admin.Settings.Manage |
| GET | `/api/v1/departments`, `/departments/lookup`, `/departments/{id}` | Admin.Departments.View |
| POST / PUT / PATCH status | `/api/v1/departments[/{id}[/status]]` | Admin.Departments.Manage |
| GET | `/api/v1/designations`, `/designations/lookup` | Admin.Designations.View |
| POST / PUT / PATCH status | `/api/v1/designations[/{id}[/status]]` | Admin.Designations.Manage |
| GET | `/api/v1/number-sequences` | Admin.Settings.Manage |
| PUT | `/api/v1/number-sequences/{key}` and `/{key}/next-value` | Admin.Settings.Manage |

Error codes: `DEPARTMENT_CODE_TAKEN`, `DEPARTMENT_NAME_TAKEN`, `DESIGNATION_NAME_TAKEN` (409); `DEPARTMENT_DEPTH`, `DEPARTMENT_HAS_SUB_DEPARTMENTS`, `DEPARTMENT_HAS_ACTIVE_SUB_DEPARTMENTS`, `DEPARTMENT_PARENT_INACTIVE`, `DEPARTMENT_NOT_FOUND`, `LOGO_INVALID`, `SEQUENCE_RESET_NEEDS_YEAR`, `SEQUENCE_CANNOT_DECREASE`, `SEQUENCE_PAD_LENGTH` (422).

Cross-module contracts (`BuildingBlocks.Contracts.Administration`): `IDepartmentLookup`, `IDesignationLookup`, `IHospitalSettingsProvider`, `INumberSequenceService` (`NextAsync("PATIENT_MRN")`), `INumberSequenceDefinitionProvider`.

### HR (implemented)
| Method | Route | Permission |
|---|---|---|
| GET | `/api/v1/employees?search&departmentId&designationId&status&employmentType&sortBy&pageNumber&pageSize` | HR.Employees.View |
| GET | `/api/v1/employees/lookup?search&take` (current employees, for pickers) | HR.Employees.View |
| GET | `/api/v1/employees/{id}` | HR.Employees.View |
| POST | `/api/v1/employees` (code generated) | HR.Employees.Manage |
| PUT | `/api/v1/employees/{id}` | HR.Employees.Manage |
| PATCH | `/api/v1/employees/{id}/status` (`status`, `exitDate`) | HR.Employees.Manage |

Doctor profiles (HR extension of Employee):

| Method | Route | Permission |
|---|---|---|
| GET | `/api/v1/doctors?search&departmentId&isAcceptingAppointments&pageNumber&pageSize` | HR.Doctors.View |
| GET | `/api/v1/doctors/{id}` | HR.Doctors.View |
| POST | `/api/v1/doctors` (employee must have a Doctor-category designation) | HR.Doctors.Manage |
| PUT | `/api/v1/doctors/{id}` (specialization, qualifications, BMDC number, new / follow-up fee, accepting appointments) | HR.Doctors.Manage |

Doctor error codes: `DOCTOR_PROFILE_EXISTS`, `DOCTOR_REGISTRATION_TAKEN` (409); `DOCTOR_EMPLOYEE_INVALID`, `DOCTOR_EMPLOYEE_NOT_DOCTOR` (422). Contract: `IDoctorLookup` in `BuildingBlocks.Contracts.HR`.

Error codes: `EMPLOYEE_DEPARTMENT_INVALID`, `EMPLOYEE_DESIGNATION_INVALID`, `EMPLOYEE_DOB_AFTER_JOINING`, `EMPLOYEE_JOINING_AFTER_EXIT`, `EMPLOYEE_EXIT_DATE_REQUIRED`, `EMPLOYEE_EXIT_BEFORE_JOINING` (422); `DEPARTMENT_IN_USE` (422, from Administration). Identity: `EMPLOYEE_INVALID` (422), `EMPLOYEE_ALREADY_LINKED` (409).

**Validation responses (all modules):** when a request body cannot be read (bad JSON, unknown enum value), the API returns 400 with `"The value is not valid."` on the affected field. It never returns parser text, CLR type names or byte positions.

### Patient (implemented)
| Method | Route | Permission |
|---|---|---|
| GET | `/api/v1/patients?search&status&pageNumber&pageSize` (MR prefix, mobile digits, name, exact ID) | Patient.View |
| GET | `/api/v1/patients/duplicate-candidates?fullName&mobileNumber&dateOfBirth&gender&identificationType&identificationNumber&excludePatientId` | Patient.View |
| GET | `/api/v1/patients/districts` | Patient.View |
| GET | `/api/v1/patients/{id}`, `/api/v1/patients/by-mrn/{mrNumber}` | Patient.View |
| POST | `/api/v1/patients` (MR number generated) | Patient.Create |
| PUT | `/api/v1/patients/{id}` | Patient.Update |
| PATCH | `/api/v1/patients/{id}/status` | Patient.Deactivate |
| GET | `/api/v1/patients/{id}/photo` | Patient.View |
| POST / DELETE | `/api/v1/patients/{id}/photo` (PNG/JPEG/WebP ≤ 2 MB) | Patient.Update |
| GET | `/api/v1/patients/{id}/medical-info` (**each call audited**) | Patient.MedicalInfo.View |
| POST / PUT / PATCH status | `/api/v1/patients/{id}/allergies[/{allergyId}[/status]]` | Patient.MedicalInfo.Manage |
| POST / PUT | `/api/v1/patients/{id}/medical-history[/{historyId}]`, `POST …/{historyId}/entered-in-error` | Patient.MedicalInfo.Manage |
| GET | `/api/v1/patients/{id}/documents`, `…/documents/{documentId}/content` | Patient.Documents.View |
| POST | `/api/v1/patients/{id}/documents` (multipart `file`, `category`, `title`; PDF/JPEG/PNG ≤ 10 MB) | Patient.Documents.Upload |
| DELETE | `/api/v1/patients/{id}/documents/{documentId}` (soft delete) | Patient.Documents.Delete |
| GET | `/api/v1/patient-merge-requests?status` | Patient.Merge.Approve |
| POST | `/api/v1/patient-merge-requests` | Patient.Merge.Request |
| POST | `/api/v1/patient-merge-requests/{id}/approve`, `/{id}/reject` | Patient.Merge.Approve |

Patient files are returned with `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`. Error codes: `PATIENT_IDENTIFICATION_DUPLICATE`, `PATIENT_MERGE_ALREADY_PENDING` (409); `PATIENT_INACTIVE`, `PATIENT_MERGED`, `PATIENT_PHOTO_INVALID`, `PATIENT_DOCUMENT_INVALID`, `PATIENT_MERGE_INVALID`, `MERGE_REQUEST_CLOSED`, `ALLERGY_ENTERED_IN_ERROR`, `HISTORY_ENTERED_IN_ERROR` (422).

Contract for other modules: `IPatientLookup` (`GetAsync`, `GetByMRNumberAsync`) in `BuildingBlocks.Contracts.Patient`.

### Appointment (implemented)
| Method | Route | Permission |
|---|---|---|
| GET | `/api/v1/doctor-schedules?doctorId` | Appointment.View |
| POST / PUT | `/api/v1/doctor-schedules[/{id}]` (weekly session: day, start/end, max patients incl. walk-ins, minutes per patient, room, effective dates) | Appointment.Schedules.Manage |
| PATCH | `/api/v1/doctor-schedules/{id}/status` | Appointment.Schedules.Manage |
| GET | `/api/v1/doctor-leaves?doctorId&from&to` | Appointment.View |
| POST / DELETE | `/api/v1/doctor-leaves[/{id}]` (whole day or one session) | Appointment.Schedules.Manage |
| GET | `/api/v1/appointments/availability?doctorId&date` | Appointment.View |
| GET | `/api/v1/appointments?date&dateFrom&dateTo&doctorId&patientId&status&pageNumber&pageSize`, `/{id}` | Appointment.View |
| POST | `/api/v1/appointments` (serial assigned under a per-session lock; `source` WalkIn = today only, checked in at once) | Appointment.Book |
| PUT | `/api/v1/appointments/{id}/reschedule` | Appointment.Book |
| POST | `/api/v1/appointments/{id}/confirm` | Appointment.Book |
| POST | `/api/v1/appointments/{id}/check-in`, `/{id}/no-show` | Appointment.CheckIn |
| POST | `/api/v1/appointments/{id}/cancel` (reason required) | Appointment.Cancel |
| POST | `/api/v1/appointments/{id}/start`, `/{id}/complete` (own patients only) | Appointment.Consult |
| GET / POST | `/api/v1/appointments/my-queue`, `/my-queue/call-next` | Appointment.Consult |
| GET | `/api/v1/queue-board` (serial numbers only, no patient data) | Appointment.QueueDisplay |

Status flow: Scheduled → Confirmed → Waiting (checked in) → InConsultation → Completed; Cancelled (until the consultation starts) and NoShow (on/after the date). Error codes: `APPOINTMENT_ALREADY_BOOKED`, `LEAVE_ALREADY_RECORDED` (409); `APPOINTMENT_DOCTOR_UNAVAILABLE`, `APPOINTMENT_PATIENT_UNAVAILABLE`, `APPOINTMENT_SESSION_INVALID`, `APPOINTMENT_SESSION_FULL`, `APPOINTMENT_SESSION_ENDED`, `APPOINTMENT_DOCTOR_ON_LEAVE`, `APPOINTMENT_DATE_OUT_OF_RANGE`, `APPOINTMENT_NOT_YOUR_PATIENT`, `APPOINTMENT_CONSULTATION_IN_PROGRESS`, `APPOINTMENT_QUEUE_EMPTY`, `APPOINTMENT_INVALID_STATUS`, `APPOINTMENT_NOT_TODAY`, `APPOINTMENT_IN_FUTURE`, `SCHEDULE_OVERLAP`, `SCHEDULE_IN_USE`, `LEAVE_HAS_APPOINTMENTS` (422); 403 when a doctor acts on another doctor's patient.

### Clinical (implemented)
OPD encounter id = the checked-in appointment id (ARCHITECTURE D16).

| Method | Route | Permission |
|---|---|---|
| GET | `/api/v1/opd/encounters/{encounterId}` (patient, allergies, history, vitals, consultation, previous visits, `canWrite`; **audited**) | Clinical.View |
| POST | `/api/v1/opd/encounters/{encounterId}/vitals` | Clinical.Vitals.Record |
| POST | `/api/v1/vital-signs/{id}/entered-in-error` (`reason`) | Clinical.Vitals.Record |
| POST | `/api/v1/opd/encounters/{encounterId}/consultation` (start or reopen; moves the appointment to InConsultation) | Clinical.Consultation.Manage |
| GET | `/api/v1/consultations/{id}` (record + patient, doctor incl. BMDC number, allergies, latest vitals; **audited**) | Clinical.View |
| PUT | `/api/v1/consultations/{id}` (whole draft; lines with `id` updated, without `id` added, missing removed; `rowVersion`) | Clinical.Consultation.Manage |
| POST | `/api/v1/consultations/{id}/finalize` (locks the record, completes the visit; safe to repeat) | Clinical.Consultation.Manage |
| POST | `/api/v1/consultations/{id}/addenda` (finalized records only) | Clinical.Consultation.Manage |
| GET | `/api/v1/patients/{patientId}/consultations` (summaries; **audited**) | Clinical.View |

Doctors may view only patients with an appointment with them; only the encounter's doctor writes. Error codes: `ENCOUNTER_NOT_ACTIVE`, `CLINICAL_NOT_A_DOCTOR`, `CONSULTATION_FINALIZED`, `CONSULTATION_NOT_FINALIZED`, `CONSULTATION_INCOMPLETE`, `CONSULTATION_DIAGNOSIS_PRIMARY`, `CONSULTATION_FOLLOW_UP_DATE`, `CONSULTATION_LINE_UNKNOWN`, `PRESCRIPTION_DURATION`, `VITALS_EMPTY`, `VITALS_BP_INCOMPLETE`, `VITALS_ALREADY_IN_ERROR` (422); `CONCURRENCY_CONFLICT` (409); 403 for access rules.

`GET /api/v1/hospital-profile/branding` now also returns `address`, `phone`, `email` (public contact details for printed letterheads).

### Billing (implemented)
All under `/api/v1/billing`. Money is `numeric(18,2)`; amounts accept at most two decimals.

| Method | Route | Permission |
|---|---|---|
| GET/POST | `/services` (paged search, `category`, `isActive`) / add | Billing.View / Billing.Services.Manage |
| PUT, PATCH | `/services/{id}` (`rowVersion`), `/services/{id}/status` | Billing.Services.Manage |
| GET | `/invoices` (paged; `search` = invoice or appointment number, `patientId`, `status`, `dueOnly`, `dateFrom`, `dateTo`) | Billing.View |
| GET | `/invoices/{id}` (lines, discounts, payments, refunds, patient advance balance) | Billing.View |
| POST, PUT | `/invoices` (draft from price-list services), `/invoices/{id}` (draft; `rowVersion`) | Billing.Invoice.Manage |
| POST | `/invoices/{id}/finalize` (`rowVersion`) | Billing.Invoice.Manage |
| POST | `/invoices/{id}/cancel` (`reason`; draft needs Invoice.Manage, finalized needs Invoice.Cancel and no cash received) | Billing.View + check |
| POST | `/invoices/{id}/discounts` (`type` Amount/Percent, `value`, `reason`) | Billing.Discount.Apply |
| POST | `/discounts/{id}/approve`, `/discounts/{id}/reject` (`note`) | Billing.Discount.Approve |
| POST | `/payments` (`clientRequestId`, `patientId`, `invoiceId` or null for an advance deposit, `amount`, `method`, `reference`) — idempotent per `clientRequestId` | Billing.Payment.Collect |
| GET | `/payments/{id}/receipt` | Billing.View |
| POST | `/refunds` (`type` AdvanceReturn with `amount`, or InvoiceCancellation with `invoiceId`; `reason`) | Billing.Refund.Request |
| POST | `/refunds/{id}/approve`, `/refunds/{id}/reject` (`note`) | Billing.Refund.Approve |
| POST | `/refunds/{id}/pay` (`method`, `reference`) | Billing.Refund.Request |
| GET | `/patients/{patientId}/account`, `/approvals`, `/reports/collections?from&to&cashierId` | Billing.View (+ Reports.View for all cashiers) |

Error codes (422 unless noted): `INVOICE_FINALIZED`, `INVOICE_NOT_FINALIZED`, `INVOICE_CANCELLED`, `INVOICE_EMPTY`, `INVOICE_LINE_UNKNOWN`, `INVOICE_LINE_INVALID`, `INVOICE_HAS_PAYMENTS`, `INVOICE_REFUND_PENDING`, `INVOICE_NOTHING_TO_REFUND`, `INVOICE_PATIENT_MISMATCH`, `PAYMENT_EXCEEDS_DUE`, `PAYMENT_AMOUNT_INVALID`, `PAYMENT_METHOD_INVALID`, `PAYMENT_REFERENCE_REQUIRED`, `PAYMENT_ADVANCE_INSUFFICIENT`, `DISCOUNT_INVALID`, `DISCOUNT_PENDING`, `DISCOUNT_EXCEEDS_DUE`, `DISCOUNT_DECIDED`, `REFUND_EXCEEDS_ADVANCE`, `REFUND_NOT_APPROVED`, `REFUND_DECIDED`, `BILLING_PATIENT_UNAVAILABLE`, `BILLING_SERVICE_UNAVAILABLE`; `SERVICE_CODE_EXISTS`, `PAYMENT_DUPLICATE_REQUEST`, `CONCURRENCY_CONFLICT` (409).

Appointment check-in (and walk-in booking) now posts the consultation fee to Billing; cancelling a checked-in appointment cancels its unpaid bill.

### IPD (implemented, Phase 4 step 1)
All under `/api/v1/ipd`.

| Method | Route | Permission |
|---|---|---|
| GET / POST / PUT / PATCH | `/bed-categories`, `/bed-categories/{id}`, `/bed-categories/{id}/active` | View / Setup.Manage |
| GET / POST / PUT / PATCH | `/wards`, `/wards/{id}`, `/wards/{id}/active` | View / Setup.Manage |
| GET / POST / PUT / PATCH | `/beds?wardId&assignableOnly`, `/beds/{id}`, `/beds/{id}/active` | View / Setup.Manage |
| PATCH | `/beds/{id}/status` (`Available`, `CleaningRequired`, `UnderMaintenance`, `Reserved`; note) | Beds.Housekeeping |
| GET | `/bed-board` | View |
| GET | `/admissions` (paged; current by default, `status`, `wardId`, `doctorId`, `patientId`, `search`), `/admissions/{id}` | View |
| POST | `/admissions` (patient, attending doctor, bed, source, reason, emergency contact) | Admit |
| POST | `/admissions/{id}/transfer`, `/admissions/{id}/doctor` (`rowVersion`) | Admit |
| POST | `/admissions/{id}/charges/sync` | View |
| PUT / POST | `/admissions/{id}/discharge-order`, `/admissions/{id}/discharge-order/cancel` | Discharge.Order |
| POST | `/admissions/{id}/final-bill`, `/admissions/{id}/release` (`overrideReason` needs Discharge.Override) | Discharge.Complete |
| GET | `/admissions/{id}/discharge-summary` (clinical access; audited) | View + clinical |

Error codes: `IPD_ALREADY_ADMITTED`, `WARD_CODE_EXISTS`, `BED_NUMBER_EXISTS` (409); `IPD_PATIENT_UNAVAILABLE`, `IPD_DOCTOR_UNAVAILABLE`, `IPD_NOT_A_DOCTOR`, `IPD_BED_INVALID`, `IPD_CATEGORY_INVALID`, `IPD_WARD_INVALID`, `BED_UNAVAILABLE`, `BED_OCCUPIED`, `WARD_OCCUPIED`, `ADMISSION_INVALID_STATUS`, `ADMISSION_TRANSFER_INVALID`, `DISCHARGE_NOT_ORDERED`, `DISCHARGE_FOLLOW_UP_DATE`, `IPD_BILL_NOT_SETTLED` (422).

Billing additions: `IChargeService.AddToRunningBillAsync / FinalizeRunningBillAsync / GetBillAsync`, `IPatientBalanceLookup`; invoice DTO `isRunningBill`, line `isPosted`; `INVOICE_RUNNING_BILL`, `INVOICE_LINE_LOCKED`, `INVOICE_NO_RUNNING_BILL`.

### Inpatient care (implemented, Phase 4 step 2)

Clinical (doctor's side):

| Method | Route | Permission |
|---|---|---|
| GET | `/ipd/admissions/{id}/chart` (stay, allergies, progress notes, medication orders, vitals; audited) | Clinical.View |
| POST | `/ipd/admissions/{id}/progress-notes`, `/progress-notes/{id}/entered-in-error` | Clinical.Inpatient.Write |
| POST | `/ipd/admissions/{id}/medication-orders`, `/medication-orders/{id}/stop` (`reason`, `rowVersion`) | Clinical.Inpatient.Write |
| POST | `/ipd/admissions/{id}/vitals` (correction: `/vital-signs/{id}/entered-in-error`) | Clinical.Vitals.Record |

Nursing (all under `/api/v1/nursing`):

| Method | Route | Permission |
|---|---|---|
| GET | `/current-shift`, `/wards/{wardId}`, `/admissions/{id}` | Nursing.View |
| GET / POST | `/admissions/{id}/mar?date=` (audited), `/admissions/{id}/mar` | View / Record |
| GET / POST | `/admissions/{id}/intake-output?date=` (8am–8am; audited), `/admissions/{id}/intake-output` | View / Record |
| GET / POST | `/admissions/{id}/notes` (audited) | View / Record |
| POST | `/mar/{id}/entered-in-error`, `/intake-output/{id}/entered-in-error`, `/notes/{id}/entered-in-error` | Record (author only) |
| GET / POST | `/handovers?wardId&date`, `/handovers` (create or revise own), `/handovers/{id}/receive` | View / Record |
| GET / POST / DELETE | `/roster?from&to&wardId` (View or Roster.Manage), `/roster`, `/roster/{id}` | Roster.Manage |

Error codes: `PROGRESS_NOTE_EMPTY`, `PROGRESS_NOTE_NOT_YOURS`, `MEDICATION_START`, `MEDICATION_DURATION`, `MEDICATION_PRN_INDICATION`, `MEDICATION_NOT_ACTIVE`, `IPD_STAY_NOT_ACTIVE`, `MAR_ORDER_INVALID`, `MAR_NOT_SCHEDULED`, `MAR_TOO_EARLY`, `MAR_REASON_REQUIRED`, `MAR_NOT_YOURS`, `NURSING_TIME_INVALID`, `FLUID_CATEGORY_INVALID`, `NURSING_NOTE_NOT_YOURS`, `HANDOVER_*`, `ROSTER_*` (422); `MAR_ALREADY_RECORDED`, `ROSTER_DUPLICATE` (409).
