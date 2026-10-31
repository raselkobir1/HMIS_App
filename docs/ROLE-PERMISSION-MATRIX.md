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
| Doctor sees full clinical history only of patients they have a relationship with (appointment, admission, referral) | Phase 2 | Manager-level check through `IDoctorPatientRelationship` contract |
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
