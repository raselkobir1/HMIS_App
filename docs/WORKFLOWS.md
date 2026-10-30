# Workflows

Cross-module workflows from BRD §5, with the owning module for each step. Each one gets a detailed state diagram when its phase is approved.

## Patient registration (Phase 1) — detailed

```mermaid
flowchart TD
    A[Receptionist opens Register Patient] --> B[Enters name / mobile / DOB / gender]
    B --> C{Live duplicate check<br/>GET /patients/duplicate-candidates}
    C -- candidates found --> D[Show possible matches]
    D -- "Existing patient" --> E[Open existing profile — no new record]
    D -- "Different person" --> F[Continue registration]
    C -- none --> F
    F --> G[Submit POST /patients]
    G --> H{Server: validation + exact ID-number check}
    H -- fail --> B
    H -- ok --> I[Begin tx → next MR number<br/>admin.NumberSequences FOR UPDATE]
    I --> J[Insert patient + audit row → commit]
    J --> K[Show profile · print ID card]
```

## Patient merge (Phase 1)

```mermaid
stateDiagram-v2
    [*] --> Pending: Receptionist requests merge (source → target, reason)
    Pending --> Approved: Hospital Admin approves
    Pending --> Rejected: Hospital Admin rejects
    Approved --> [*]: Source status = Merged, MergedIntoPatientId = target, audited
    Rejected --> [*]
```

Records in other modules that point to the source patient are reassigned later through the `PatientMerged` event, once those modules exist.

## Cross-module overview (later phases)

| Workflow | Steps → owning module |
|---|---|
| OPD (P2) | Registration [Patient] → Appointment & Queue [Appointment] → Visit [OPD] → Consultation, Diagnosis, Prescription, Investigation [Clinical] → Charge [Billing] → Payment [Billing] → Completion [OPD] |
| IPD (P4) | Registration [Patient] → Admission + Bed [IPD] → Assessment / Treatment [Clinical] → Nursing [Nursing] → Investigations [Lab/Radiology] → Pharmacy → Daily charges [Billing] → Discharge summary [Clinical/IPD] → Final bill & payment [Billing] → Discharge [IPD] |
| Laboratory (P5) | Order [Clinical] → Authorization/billing [Billing] → Sample → Processing → Result → Verification → Publication [Lab] |
| Pharmacy (P6) | Prescription [Clinical] → Queue → Availability → FEFO batch → Dispense → Stock deduction [Inventory] → Invoice/Payment [Billing] |
| Procurement (P6) | Requisition → Approval → PO → GRN [Procurement] → Stock [Inventory] → Supplier invoice → Payment → Voucher [Finance] |
| Asset (P7) | Purchase → Registration → Assignment → Maintenance → Repair → Depreciation → Disposal [Asset] |

**Safety rules carried into every workflow:** payment must never block emergency care or a clinically necessary discharge; this requires an authorized exception path (BRD §5, §6).
