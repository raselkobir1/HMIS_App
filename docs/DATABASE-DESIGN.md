# Database Design

**Status:** Initial ERD for Phase 0–1 schemas. Proposed — awaiting approval.  
**Engine:** PostgreSQL 17, single database `hmis`, one schema per module.  
**Conventions:** PascalCase tables and columns, `uuid` primary keys (v7), `timestamptz` stored in UTC, `numeric(18,2)` for money, `xmin` for optimistic concurrency, and unique constraints on every business identifier.

Later-phase schemas will be designed in detail when each module is approved. The BRD §9 domain map is the starting point.

---

## 1. Schema Map

| Schema | Module | Phase |
|---|---|---|
| `auth` | Identity | 0 |
| `audit` | Audit | 0/1 |
| `admin` | Administration | 1 |
| `hr` | HR | 1 (basic) |
| `patient` | Patient | 1 |
| `appointment`, `clinical`, `opd` | Appointment, Clinical, OPD | 2 |
| `billing` | Billing | 3 |
| `ipd`, `nursing` | IPD + Bed, Nursing | 4 |
| `lab`, `radiology` | Laboratory, Radiology | 5 |
| `pharmacy`, `inventory`, `procurement` | — | 6 |
| `asset` | Asset | 7 |
| `emergency`, `ot`, `bloodbank` | — | 8 |
| `finance`, `reporting` | — | 9 |

---

## 2. Common Columns

| Column | Type | On |
|---|---|---|
| `Id` | uuid PK | every table |
| `CreatedAt`, `CreatedBy` | timestamptz, uuid | auditable entities |
| `ModifiedAt`, `ModifiedBy` | timestamptz null, uuid null | mutable auditable entities |
| `IsActive` | boolean | reference/master data (deactivate instead of delete) |
| `xmin` | system column | concurrency token on mutable aggregates |

`CreatedBy` and `ModifiedBy` store the user ID **without** a cross-schema foreign key. This keeps modules independent of `auth`.

---

## 3. ERD — `auth` (Identity)

```mermaid
erDiagram
    Users ||--o{ UserRoles : has
    Roles ||--o{ UserRoles : has
    Roles ||--o{ RolePermissions : grants
    Permissions ||--o{ RolePermissions : in
    Users ||--o{ UserPermissions : "direct grant"
    Permissions ||--o{ UserPermissions : in
    Users ||--o{ RefreshTokens : owns
    Users ||--o{ LoginHistory : logs

    Users {
        uuid Id PK
        string UserName UK
        string Email UK
        string PhoneNumber
        string FullName
        string PasswordHash
        uuid EmployeeId "nullable, ref hr.Employees (no FK)"
        bool IsActive
        bool MustChangePassword
        int AccessFailedCount
        timestamptz LockoutEnd
        string SecurityStamp
        timestamptz LastLoginAt
    }
    Roles {
        uuid Id PK
        string Name UK
        string Description
        bool IsSystem "Super Administrator cannot be deleted"
    }
    Permissions {
        uuid Id PK
        string Code UK "e.g. Patient.View"
        string Module
        string Description
    }
    UserRoles { uuid UserId PK  uuid RoleId PK }
    RolePermissions { uuid RoleId PK  uuid PermissionId PK }
    UserPermissions { uuid UserId PK  uuid PermissionId PK }
    RefreshTokens {
        uuid Id PK
        uuid UserId FK
        string TokenHash UK
        uuid FamilyId
        timestamptz ExpiresAt
        timestamptz RevokedAt
        uuid ReplacedByTokenId
        string CreatedByIp
    }
    LoginHistory {
        uuid Id PK
        uuid UserId "nullable for unknown user"
        string UserNameAttempted
        bool Succeeded
        string FailureReason
        string IpAddress
        string UserAgent
        timestamptz AttemptedAt
    }
```

The other ASP.NET Identity tables (claims, logins, tokens) are only created if they are needed.

---

## 4. ERD — `audit`

```mermaid
erDiagram
    AuditLogs {
        uuid Id PK
        uuid UserId
        string UserName
        string Action "Create/Update/Delete/View/Login/..."
        string Module
        string EntityName
        string EntityId
        timestamptz Timestamp
        string IpAddress
        string TraceId
        jsonb OldValues
        jsonb NewValues
    }
```

The table is append-only: there is no update or delete path in the application, and trigger `TR_AuditLogs_AppendOnly` rejects `UPDATE`, `DELETE` and `TRUNCATE`. `OldValues` and `NewValues` are `jsonb`. Indexes: `(Timestamp DESC)`, `(EntityName, EntityId)`, `(UserId, Timestamp)`, `(Module, Timestamp)`.

---

## 5. ERD — `admin` (Administration)

```mermaid
erDiagram
    HospitalProfile {
        uuid Id PK "single row"
        string Name
        string NameBangla
        string LogoFileId
        string Address
        string Phone
        string Email
        string Website
        string RegistrationNo
        string LicenseNo
        string TaxId
        string OpeningHours
    }
    SystemSettings {
        string Key PK
        string Value
        string Description
    }
    Departments ||--o{ Departments : "sub-department"
    Departments {
        uuid Id PK
        string Code UK
        string Name UK
        uuid ParentDepartmentId FK "nullable"
        string Type "Clinical/Diagnostic/Administrative/Support"
        bool IsActive
    }
    Designations {
        uuid Id PK
        string Name UK
        string Category "Doctor/Nurse/Technician/Admin/Support"
        bool IsActive
    }
    NumberSequences {
        uuid Id PK
        string Key UK "PATIENT_MRN, INVOICE, APPOINTMENT..."
        string Prefix
        string Format "e.g. {PREFIX}{YYYY}{SEQ:6}"
        long CurrentValue
        string ResetPolicy "Never/Yearly/Monthly"
        int PeriodYear
    }
```

**As built:** `SystemSettings` is a typed single-row table (TimeZone fixed to `Asia/Dhaka`, CurrencyCode, CurrencySymbol, FinancialYearStartMonth with a 1–12 check), not a key/value table. `HospitalProfile` stores `LogoPath`/`LogoContentType` (file in `IFileStorageService`, not in the database). `Departments` have a unique `Code` and `NormalizedName`, and a self-referencing FK limited to two levels by the domain. `Designations` have a unique `NormalizedName`. Enums are stored as strings.  
`NumberSequences` allocation is a single `UPDATE … SET "CurrentValue" = … RETURNING` statement (row lock, yearly reset in the same statement), run on the Administration connection. Numbers are unique, but gaps are possible if the caller's own transaction later fails. `CurrentValue >= 0` is enforced by a check constraint.

---

## 6. ERD — `hr` (basic, Phase 1)

```mermaid
erDiagram
    Employees {
        uuid Id PK
        string EmployeeCode UK
        string FullName
        string Gender
        date DateOfBirth
        string MobileNumber
        string Email
        string Address
        uuid DepartmentId "ref admin.Departments"
        uuid DesignationId "ref admin.Designations"
        string EmploymentType "Permanent/Contract/Visiting/Intern"
        date JoiningDate
        string Status "Active/OnLeave/Resigned/Terminated"
    }
```

**As built:** adds `ExitDate` (required when the status is Resigned/Terminated, on or after joining). `EmployeeCode` is unique and generated from the `EMPLOYEE_CODE` sequence. Gender, EmploymentType and Status are stored as strings. Indexes: `(DepartmentId, Status)`, `DesignationId`, `MobileNumber`, `FullName`. `auth.Users.EmployeeId` has a unique filtered index (one login per employee).  
Attendance, leave, payroll, documents and the doctor profile are added in later phases.

---

## 7. ERD — `patient` (reference module)

```mermaid
erDiagram
    Patients ||--o{ PatientEmergencyContacts : has
    Patients ||--o{ PatientAllergies : has
    Patients ||--o{ PatientMedicalHistories : has
    Patients ||--o{ PatientDocuments : has
    Patients ||--o{ PatientMergeRequests : "source/target"

    Patients {
        uuid Id PK
        string MRNumber UK
        string FullName
        string FullNameBangla "optional"
        string Gender "Male/Female/Other"
        date DateOfBirth "nullable"
        bool IsDobEstimated
        string BloodGroup "nullable"
        string MobileNumber
        string Email
        string PresentAddress "line, area, district, postcode"
        string PermanentAddress
        string IdentificationType "NID/BirthReg/Passport/None"
        string IdentificationNumber
        string GuardianName
        string GuardianRelation
        string PhotoFileId
        string Status "Active/Inactive/Merged/Deceased"
        uuid MergedIntoPatientId "nullable"
    }
    PatientEmergencyContacts {
        uuid Id PK
        uuid PatientId FK
        string Name
        string Relation
        string MobileNumber
    }
    PatientAllergies {
        uuid Id PK
        uuid PatientId FK
        string Allergen
        string AllergyType "Drug/Food/Environmental/Other"
        string Severity "Mild/Moderate/Severe"
        string Reaction
        string Status "Active/Resolved/EnteredInError"
    }
    PatientMedicalHistories {
        uuid Id PK
        uuid PatientId FK
        string Category "PastIllness/Surgical/Family/Social/CurrentMedication"
        string Description
        date OnsetDate
    }
    PatientDocuments {
        uuid Id PK
        uuid PatientId FK
        string Category
        string FileName
        string StoragePath
        string MimeType
        long SizeBytes
        string Sha256
    }
    PatientMergeRequests {
        uuid Id PK
        uuid SourcePatientId FK
        uuid TargetPatientId FK
        string Reason
        string Status "Pending/Approved/Rejected"
        uuid RequestedBy
        uuid ReviewedBy
        timestamptz ReviewedAt
    }
```

**Indexes:**
- `MRNumber` (unique) and `MobileNumber`.
- A `pg_trgm` GIN index on `FullName` for fuzzy search and duplicate detection.
- `(IdentificationType, IdentificationNumber)`: unique where not null. *Needs confirmation.*

Patient visits, admission history, billing and lab history are **not** stored in this schema. The patient profile pulls them from the owning modules through contracts.

> Field lists for Patient depend on answers in `OPEN-QUESTIONS.md` (mandatory fields, MR format, ID types, address structure).
