# Role & Permission Matrix

**Status:** Proposed — awaiting approval  
**Model:** A user can have many roles. A role grants many permissions. A permission can also be granted to a user directly. Roles and grants are **configurable data**; the defaults below are only seed values. The backend checks every endpoint.

Permission code format: `<Module>.<Action>`, for example `Patient.Create`.

---

## 1. Default Roles (seeded)

| Role | System role? | Notes |
|---|---|---|
| Super Administrator | Yes (cannot be deleted; always has every permission) | System configuration |
| Hospital Administrator | No | Operations; **no automatic clinical access** (BRD §11) |
| Receptionist | No | |
| Doctor | No | |
| Nurse | No | |
| Pharmacist | No | Phase 6 |
| Laboratory Technician | No | Phase 5 |
| Radiologist | No | Phase 5 |
| Accountant / Cashier | No | Phase 3 |
| HR Manager | No | |
| Store Manager | No | Phase 6 |
| Hospital Owner / Management | No | Read-only dashboards and reports |

---

## 2. Phase 0–1 Permissions × Default Roles

Legend: ✔ granted by default · — not granted

| Permission | Super Admin | Hosp. Admin | Reception | Doctor | Nurse | HR Mgr | Owner |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **Identity** |||||||
| `Identity.Users.View` | ✔ | ✔ | — | — | — | ✔ | — |
| `Identity.Users.Manage` (create/update/activate/reset password) | ✔ | — | — | — | — | — | — |
| `Identity.Roles.View` | ✔ | ✔ | — | — | — | — | — |
| `Identity.Roles.Manage` (roles + permission assignment) | ✔ | — | — | — | — | — | — |
| `Identity.LoginHistory.View` | ✔ | ✔ | — | — | — | — | — |
| **Audit** |||||||
| `Audit.View` | ✔ | — | — | — | — | — | — |
| **Administration** |||||||
| `Admin.HospitalProfile.View` | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| `Admin.HospitalProfile.Manage` | ✔ | ✔ | — | — | — | — | — |
| `Admin.Departments.View` | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| `Admin.Departments.Manage` | ✔ | ✔ | — | — | — | — | — |
| `Admin.Designations.View` | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| `Admin.Designations.Manage` | ✔ | ✔ | — | — | — | ✔ | — |
| `Admin.Settings.Manage` (numbering, timezone, currency) | ✔ | — | — | — | — | — | — |
| **HR (basic)** |||||||
| `HR.Employees.View` | ✔ | ✔ | — | — | — | ✔ | ✔ |
| `HR.Employees.Manage` | ✔ | — | — | — | — | ✔ | — |
| **Patient** |||||||
| `Patient.View` (demographics, search) | ✔ | ✔ | ✔ | ✔ | ✔ | — | — |
| `Patient.Create` | ✔ | — | ✔ | — | — | — | — |
| `Patient.Update` | ✔ | — | ✔ | — | — | — | — |
| `Patient.Deactivate` | ✔ | ✔ | — | — | — | — | — |
| `Patient.Merge.Request` | ✔ | — | ✔ | — | — | — | — |
| `Patient.Merge.Approve` | ✔ | ✔ | — | — | — | — | — |
| `Patient.MedicalInfo.View` (allergies, medical history) | ✔ | — | — | ✔ | ✔ | — | — |
| `Patient.MedicalInfo.Manage` | ✔ | — | — | ✔ | ✔ | — | — |
| `Patient.Documents.View` | ✔ | — | ✔ | ✔ | ✔ | — | — |
| `Patient.Documents.Upload` | ✔ | — | ✔ | ✔ | ✔ | — | — |
| `Patient.Documents.Delete` | ✔ | — | — | — | — | — | — |

> **Super Admin and clinical data:** BRD §11 says administrative visibility must not automatically grant clinical access. Super Admin is currently shown with every permission. **Open question Q-SEC-1:** should Super Admin be excluded from `Patient.MedicalInfo.*`?

---

## 3. Data-Level Restrictions (later phases)

| Restriction | Applies from | Mechanism |
|---|---|---|
| Doctor sees full clinical history only of patients they have a relationship with (appointment, admission, referral) | Phase 2 — **implemented** for appointments | `ClinicalManager` checks `IOpdEncounterService.HasRelationshipAsync` (any non-cancelled appointment) for logins linked to a doctor; other clinical staff (e.g. nurses) are governed by permissions only |
| Nurse sees assigned ward/patients | Phase 4 | Ward-assignment check |
| Department-scoped access | Phase 2+ | User → Employee → Department |
| "Break-the-glass" emergency access to any record, with mandatory reason + audit | Phase 8 (Emergency) | *Needs approval* |

## 4. Module-Level Access Overview (all phases, default intent)

| Module | Super Admin | Hosp. Admin | Reception | Doctor | Nurse | Pharmacist | Lab Tech | Radiologist | Cashier | HR | Store | Owner |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Administration | Full | Manage | View | View | View | View | View | View | View | View | View | View |
| Patient | Full | View | Manage | View+Medical | View+Medical | View | View | View | View | — | — | — |
| Appointment / Queue | Full | View | Manage | Own | — | — | — | — | View | — | — | Report |
| OPD / Clinical | Full | — | Create visit | Manage own | Vitals | Rx view | — | — | — | — | — | Report |
| IPD / Bed | Full | View | Admit | Manage | Care | — | — | — | View | — | — | Report |
| Nursing | Full | — | — | View | Manage | — | — | — | — | — | — | — |
| Laboratory | Full | — | Order view | Order/Result view | — | — | Manage | — | — | — | — | Report |
| Radiology | Full | — | — | Order/Report view | — | — | — | Manage | — | — | — | Report |
| Pharmacy | Full | — | — | Rx | MAR view | Manage | — | — | — | — | Stock view | Report |
| Billing | Full | View | Collect (OPD) | — | — | Pharmacy sales | — | — | Manage | — | — | Report |
| Finance | Full | — | — | — | — | — | — | — | Manage | — | — | Report |
| HR | Full | View | — | — | — | — | — | — | — | Manage | — | Report |
| Inventory / Procurement | Full | Approve | — | — | — | Request | Request | — | — | — | Manage | Report |
| Asset | Full | Manage | — | — | — | — | — | — | — | — | View | Report |
| Reports / Dashboard | Full | Ops | — | Own | — | — | — | — | Finance | HR | Inventory | All |

Detailed permission codes for each later module will be added here when that module is approved.

> **As built (Administration):** the `*.View` permissions for hospital profile, departments and designations are granted by default to every staff role (all roles except the Super Administrator, who receives everything automatically). Pharmacist, Laboratory Technician, Radiologist, Accountant/Cashier and Store Manager are not shown as columns above but also receive them.

## Clinical permissions (implemented, Phase 2)

| Permission | Default roles | Notes |
|---|---|---|
| `Clinical.View` | Doctor, Nurse | Explicit grant (not given to Super Administrator automatically). Every workspace, consultation and history view is audited. |
| `Clinical.Vitals.Record` | Doctor, Nurse | Explicit grant. Only while the patient is checked in or in consultation. |
| `Clinical.Consultation.Manage` | Doctor | Explicit grant. Only the encounter's own doctor may open, edit, finalize or amend. |

## Billing permissions (implemented, Phase 3)

| Permission | Default roles | Notes |
|---|---|---|
| `Billing.View` | Accountant / Cashier, Hospital Administrator | Invoices, patient accounts, approvals list, own collection report |
| `Billing.Services.Manage` | Hospital Administrator | Price list |
| `Billing.Invoice.Manage` | Accountant / Cashier | Create, edit, finalize and discard draft invoices |
| `Billing.Invoice.Cancel` | Hospital Administrator | Cancel a finalized invoice with no cash received |
| `Billing.Payment.Collect` | Accountant / Cashier | Payments, advance deposits, applying advance |
| `Billing.Discount.Apply` | Accountant / Cashier | Applies at once up to 10% of the subtotal in total; above that it waits for approval |
| `Billing.Discount.Approve` | Hospital Administrator | A user holding both Apply and Approve gets larger discounts applied at once |
| `Billing.Refund.Request` | Accountant / Cashier | Request refunds and pay out approved ones |
| `Billing.Refund.Approve` | Hospital Administrator | A requester who may approve is approved at once (still paid out separately) |
| `Billing.Reports.View` | Accountant / Cashier, Hospital Administrator, Hospital Owner | Collection report for all cashiers; without it a user sees only their own collection |

`Patient.View` now also defaults to Accountant / Cashier (the cash counter must find patients). Defaults apply when a permission is first created; existing databases grant it on the Roles screen.
