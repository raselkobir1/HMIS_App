# UI/UX Navigation Structure

**Status:** Proposed — awaiting approval  
**Layout:** `admin-layout` uses a collapsible left sidenav, a top toolbar (hospital name, global patient search, notifications, user menu) and a breadcrumb.  
Menu items are shown only when the user has the required permission. This is a UI convenience; the backend enforces all access.  
**(Pn)** = the phase in which the item becomes active. Items for later phases stay hidden until then.

```text
Dashboard (P1: basic counts; P9: executive dashboard)

Patients (P1)
├── Register Patient                 Patient.Create
├── Search Patients                  Patient.View
├── Merge Requests                   Patient.Merge.Request / Approve
└── Patient Profile (from search)    tabs: Overview · Medical Info · Documents · Visits* · Admissions* · Billing* · Lab* 
                                     (*tabs appear when the owning module exists)

Appointments (P2)
├── Book Appointment
├── Appointment List / Calendar
├── Queue Board
└── Doctor Schedules

OPD (P2)
├── New OPD Visit
├── My Consultations (Doctor)
└── OPD Visit List

IPD (P4)
├── Admit Patient
├── Admitted Patients
├── Bed Dashboard
├── Transfers
└── Discharges

Emergency (P8)
Nursing (P4)           Ward Patients · Vitals · MAR · Handover
Laboratory (P5)        Orders · Sample Collection · Result Entry · Verification · Reports · Test Master
Radiology (P5)         Requests · Reports · Templates · Service Master
Pharmacy (P6)          Dispense · Direct Sale · Returns · Medicine Master · Stock · Expiry Alerts
Billing (P3)           New Invoice · Invoices · Payments · Refunds · Discount Approvals · Cashier Closing · Service & Price Master
Inventory & Procurement (P6)
HR (P1 basic / P7)     Employees · (P7: Attendance · Leave · Shifts · Payroll)
Assets (P7)
OT · Blood Bank · Ambulance (P8)
Accounts (P9)          Chart of Accounts · Vouchers · Ledgers · Trial Balance · Statements
Reports (P3+)          Patient · Clinical · Financial · Inventory · HR

Administration (P1)
├── Hospital Profile                 Admin.HospitalProfile.Manage
├── Departments                      Admin.Departments.Manage
├── Designations                     Admin.Designations.Manage
└── System Settings & Numbering      Admin.Settings.Manage

Security (P0)
├── Users                            Identity.Users.*
├── Roles & Permissions              Identity.Roles.*
├── Login History                    Identity.LoginHistory.View
└── Audit Log                        Audit.View

User menu: My Profile · Change Password · Logout
```

## Screens in Phase 0–1

| Screen | Key UX requirements |
|---|---|
| Login | Username/email + password; lockout message; forced password change on first login |
| Users list / form | Server-side paged table; assign roles; activate/deactivate; reset password |
| Roles & permissions | Role list; permission checklist grouped by module |
| Hospital profile | Single form + logo upload |
| Departments | Tree/table with parent; activate/deactivate |
| Employees | Paged table + form |
| Patient registration | Single-page keyboard-friendly form; **live duplicate warning** while typing mobile/name/DOB; print ID card after save |
| Patient search | Search by MR number / mobile / name / ID number; paged results |
| Patient profile | Header with MR, name, age, gender, blood group and a **prominent allergy banner**; tabs |

## Implemented menu (Phase 2, step 1)
- Appointments: Book Appointment (Appointment.Book), Appointments (Appointment.View), My Queue (Appointment.Consult), Doctor Sessions (Appointment.View; editing needs Appointment.Schedules.Manage), Queue Display (Appointment.QueueDisplay — opens `/queue-display` full screen for a waiting-room TV).
- HR: Doctors (HR.Doctors.View).
- Patient profile: "Book appointment" button and Appointments tab.

## Implemented menu (Phase 2, step 2)
- OPD: OPD Vitals (Clinical.Vitals.Record) — today's checked-in patients for the nurse station.
- My Queue: "Call next" / "See now" / "Open consultation" lead to the consultation screen `/opd/encounters/{appointmentId}` for doctors with Clinical.Consultation.Manage.
- Consultation screens: `/opd/consultations/{id}` (read-only record) and `/opd/consultations/{id}/print` (A4 prescription).
- Patient profile: Visits tab (Clinical.View; loaded only when opened, because viewing is audited).

## Implemented menu (Phase 3)
- Billing: Invoices (Billing.View) · New Invoice (Billing.Invoice.Manage) · Approvals (Discount.Approve, Refund.Approve or Refund.Request) · Collections (Billing.View) · Price List (Billing.View; editing needs Services.Manage).
- Screens: `/billing/invoices/{id}` (actions by status and permission), `/billing/invoices/{id}/edit` (drafts), `/billing/patients/{patientId}` (account), `/billing/invoices/{id}/print` and `/billing/receipts/{paymentId}/print` (A5).
- Patient profile: "Billing" button (Billing.View) opens the patient's account. Not yet done: cashier closing (planned with Finance).

## Implemented menu (Phase 4 step 1)
- IPD: Bed Board (Ipd.View) · Admissions (Ipd.View) · Admit Patient (Ipd.Admit) · Ward & Bed Setup (Ipd.Setup.Manage).
- Screens: `/ipd/admissions/{id}` (stay, bill and deposit, discharge summary, actions by status), `/ipd/admissions/{id}/summary` (A4 print).
- Patient profile: "Admit" button (Ipd.Admit). Billing invoice page marks IPD running bills; their bed lines are read-only.

## Implemented menu (Phase 4 step 2)
- Nursing: Ward Nursing (Nursing.View) · Shift Handover (Nursing.View) · Nurse Roster (Nursing.View or Nursing.Roster.Manage).
- Screens: `/nursing/patients/{admissionId}` — patient chart with tabs MAR, Orders & notes, Vitals, Intake / output, Nursing notes (tabs follow permissions; doctors open on Orders & notes, nurses on the MAR). The IPD admission page links to it ("Patient chart").

## Implemented (gap closure)
- Toolbar bell (all users). Dashboard cards by permission: patients today, appointments today, bed occupancy, collections today, approvals waiting, ward nursing.
- Billing → Billing Settings (Billing.Settings.Manage). Administration → Notification Settings (Notifications.Settings.Manage).
- Patient profile: Admissions tab (Ipd.View), deceased and merged-record banners.
