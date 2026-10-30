# Hospital Management Information System (HMIS)
## Business Requirements Document (BRD) & Product Development Blueprint

**Document Version:** 1.0  
**Document Type:** Business Requirements Document + Product Blueprint  
**Target Market:** Private Hospitals in Bangladesh  
**Primary Objective:** Complete digital hospital operations and paperless management  
**Status:** Initial business specification

---
I recommend starting with Patient Management, Doctor Management, Appointment, OPD, Billing, IPD, Bed Management, and User Management. These establish the core operational foundation.

After that, integrate Pharmacy, Laboratory, Nursing, Inventory, HR, Assets, and Accounting.

# 1. Executive Summary

## 1.1 Business Overview

The proposed Hospital Management Information System (HMIS) is a centralized, web-based application designed to digitize and automate the clinical, operational, administrative, and financial activities of a private hospital.

The system replaces disconnected paper registers, spreadsheets, and manual workflows with a secure, integrated platform. It must provide a reliable source of information for patients, healthcare professionals, hospital resources, services, inventory, billing, and management reporting.

Common problems the system should address:

- Difficulty maintaining patient information and medical history.
- Manual appointment scheduling and doctor availability management.
- Limited visibility into doctors, nurses, employees, and their responsibilities.
- Inefficient bed, cabin, and ward allocation.
- Paper-based prescriptions and diagnostic reports.
- Difficulty tracking medicine, consumable, and equipment stock.
- Billing errors and incomplete payment tracking.
- Limited visibility into income, expenses, and outstanding balances.
- Slow retrieval of historical patient and operational information.
- Lack of accountability and traceability for important changes.

## 1.2 Product Vision

Build a professional, secure, scalable, user-friendly HMIS that connects hospital departments through one integrated platform.

The system should enable:

1. Paperless patient registration and medical documentation.
2. Real-time operational visibility.
3. Consistent and automated hospital workflows.
4. Centralized employee and resource management.
5. Accurate billing and financial tracking.
6. Secure electronic medical records (EMR).
7. Data-driven management decisions.
8. Future integration with external healthcare systems.

## 1.3 Business Goals

| Goal | Expected Outcome |
|---|---|
| Paperless operations | Reduce dependence on physical registers and forms |
| Centralized information | Establish one reliable source of operational data |
| Operational efficiency | Reduce repetitive manual work |
| Patient service | Improve registration, appointments, and billing |
| Clinical documentation | Maintain accessible, traceable electronic records |
| Financial control | Track invoices, payments, refunds, and expenses |
| Inventory control | Monitor medicines and medical supplies |
| Management visibility | Provide timely dashboards and reports |
| Accountability | Record important user activities and changes |

---

# 2. System Scope

The target system includes the following business domains:

| Domain | Main Responsibilities |
|---|---|
| Administration | Hospital configuration and operations |
| Patient Management | Patient registration and patient information |
| Appointment Management | Doctor schedules, appointments, and queues |
| OPD | Outpatient visits and consultations |
| IPD | Inpatient admissions, treatment, and discharge |
| Emergency | Emergency patient handling |
| Clinical / EMR | Medical history, diagnosis, and prescriptions |
| Nursing | Nursing assessments and care activities |
| Pharmacy | Medicine dispensing and inventory |
| Laboratory | Test orders, samples, and results |
| Radiology | Imaging orders and reports |
| Operation Theatre | Surgical scheduling and records |
| Blood Bank | Donor, blood stock, and transfusion records |
| Bed Management | Wards, rooms, cabins, and beds |
| HR | Doctors, nurses, and employee management |
| Asset Management | Medical and non-medical equipment |
| Procurement | Purchasing, suppliers, and stores |
| Billing | Patient invoices and payment management |
| Finance | Accounting and financial reporting |
| Reporting | Operational and financial analytics |
| Security | Authentication, authorization, and audit |

The hospital should activate only the modules it actually operates. The architecture should allow additional modules to be introduced later.

---

# 3. User Roles and Responsibilities

The system must support Role-Based Access Control (RBAC). A user may have multiple roles, and permissions must be configurable rather than hardcoded.

## 3.1 Main Roles

### Super Administrator
- Manage system-wide settings.
- Configure departments and hospital structure.
- Create users, roles, and permissions.
- Monitor system activities.
- Manage integrations and configurations.

### Hospital Administrator
- Manage hospital operations.
- Monitor departments.
- Review operational reports.
- Manage hospital services.
- Monitor employee activities.

### Receptionist
- Register and search patients.
- Create appointments and OPD visits.
- Manage patient queues.
- Generate registration receipts.

### Doctor
- View assigned appointments and relevant patient history.
- Record clinical observations and diagnosis.
- Create electronic prescriptions.
- Request laboratory and radiology investigations.
- Record treatment plans and follow-up instructions.
- Review diagnostic reports.

### Nurse
- View assigned patients.
- Record vital signs and nursing notes.
- Record medication administration.
- Monitor patient conditions.
- Maintain nursing care plans and handover information.

### Pharmacist
- Manage medicine inventory and batches.
- Process prescriptions and dispense medicines.
- Track expiry dates and stock movements.
- Handle authorized medicine returns.

### Laboratory Technician
- View laboratory orders.
- Manage sample collection and processing.
- Enter test results.
- Submit results for verification.

### Radiologist
- Review imaging requests.
- Record imaging findings.
- Upload and verify diagnostic reports.

### Accountant / Cashier
- Manage patient billing and collections.
- Record payments and refunds.
- Monitor outstanding balances.
- Manage financial transactions according to permissions.

### HR Manager
- Manage employee records, attendance, leave, and payroll.

### Store Manager
- Manage inventory, purchase requests, suppliers, and stock movements.

### Hospital Owner / Management
- View hospital performance.
- Monitor revenue, expenses, patient statistics, and department performance.

## 3.2 Authorization Principles

- A user may have multiple roles.
- Permissions must be configurable.
- Access may be restricted by department, ward, assignment, or data sensitivity.
- Backend authorization is mandatory; hiding a frontend menu is not security.
- Access to sensitive patient records must be auditable.

---

# 4. Functional Requirements

## Module 01: Hospital Administration & Configuration

### Hospital Profile
- Hospital name, logo, address, and contact details.
- Registration and license information.
- Opening hours.
- Tax and billing configuration.

### Organization
- Department and sub-department management.
- Designations and employee categories.
- Buildings, floors, wards, rooms, cabins, beds, and operation theatres.
- Configurable hospital services and service categories.

### Configuration
- Patient ID and invoice numbering.
- Appointment numbering.
- Financial year and timezone.
- Currency and payment methods.
- Notification settings.
- Configurable service prices and approval limits.

## Module 02: Patient Management

### Patient Registration
Capture, as applicable:

- Permanent Patient ID / MR Number.
- Full name.
- Gender.
- Date of birth.
- Blood group.
- Mobile number and email.
- Present and permanent address.
- Emergency contact and guardian information.
- Identification information.
- Photograph, where appropriate.

### Patient Features
- New registration and existing-patient search.
- Duplicate patient detection and controlled merge process.
- Patient profile and ID card.
- Visit and admission history.
- Medical history and allergies.
- Current medications.
- Diagnoses and prescriptions.
- Laboratory and radiology history.
- Billing history.
- Securely stored medical documents.

**Business rule:** A patient has one permanent Patient ID. Multiple visits, consultations, admissions, and diagnostic orders must reference that patient instead of creating duplicate patient records.

## Module 03: Doctor & Appointment Management

### Doctor Profile
- Qualifications and specialization.
- Department and professional registration details.
- Consultation fee.
- Available days, working hours, and chamber.
- Employment and active status.

### Appointment Features
- Create, confirm, reschedule, and cancel appointments.
- Select doctor, department, date, and available time slot.
- Manage walk-in patients.
- Maintain appointment history.
- Track appointment status.

### Appointment Status
- Scheduled
- Confirmed
- Checked In
- Waiting
- In Consultation
- Completed
- Cancelled
- No Show

### Queue Management
- Generate queue numbers.
- Show waiting, in-consultation, and completed patients.
- Support queue display screens without exposing sensitive information.

## Module 04: OPD (Outpatient Department)

### Workflow
1. Patient registers or is identified.
2. Receptionist creates an OPD visit.
3. Doctor or department is selected.
4. Applicable consultation charge is generated.
5. Patient receives a queue number.
6. Doctor opens the consultation.
7. Doctor records clinical information.
8. Doctor records diagnosis and prescription.
9. Doctor may request laboratory or radiology investigations.
10. Consultation is completed and documents are made available.

### Consultation Information
- Chief complaint and history of present illness.
- Relevant previous medical history and allergies.
- Physical examination.
- Blood pressure, temperature, pulse, respiratory rate, oxygen saturation, weight, and height.
- Primary and secondary diagnosis.
- Prescription: medicine, strength, dosage, route, frequency, duration, quantity, and instructions.
- Investigation requests.
- Follow-up date and instructions.
- Referral information.

**Clinical rule:** Only authorized healthcare professionals may create, verify, or finalize clinical records according to their responsibilities. Finalized historical records must not be silently overwritten.

## Module 05: IPD (Inpatient Department)

### Admission
- Admission reason and date/time.
- Responsible doctor and department.
- Ward, room, cabin, and bed assignment.
- Emergency contact.
- Admission deposit and applicable billing arrangements.

### Inpatient Operations
- Doctor visits and progress notes.
- Nursing notes and vital signs.
- Medication administration.
- Treatment plans.
- Laboratory and radiology requests.
- Bed and doctor transfers.
- Discharge planning.

### Discharge
- Discharge request and approval.
- Final diagnosis.
- Discharge summary.
- Medication and follow-up instructions.
- Final billing and payment settlement.
- Discharge date and time.

**Safety rule:** Financial workflows must not delay emergency clinical decisions or clinically necessary discharge decisions. Authorized staff need an exception process.

## Module 06: Emergency Department

- Emergency patient registration, including temporary identity where necessary.
- Triage and severity classification.
- Vital signs and initial assessment.
- Emergency doctor assignment.
- Treatment and investigation records.
- Conversion to admission.
- Referral and discharge.
- Death documentation, where applicable.

Emergency care must not be delayed by ordinary registration or payment procedures.

## Module 07: Nursing Management

- Nurse profiles, departments, wards, and shifts.
- Patient and nurse assignments.
- Nursing assessment and care plans.
- Vital signs and patient observations.
- Medication Administration Record (MAR).
- Intake and output.
- Nursing notes and shift handover.

### Medication Administration Record
Record patient, medicine, prescribed dosage, scheduled and actual administration time, administering user, status, omission reason, and notes.

Medication administration must be linked to an authorized medication order.

## Module 08: Pharmacy & Medicine Management

### Medicine Master
- Medicine and generic name.
- Brand, category, strength, and dosage form.
- Manufacturer and unit.
- Barcode and reorder level.

### Inventory
- Purchase and supplier.
- Batch and expiry tracking.
- Purchase and selling price.
- Stock adjustment, transfer, return, and reconciliation.

### Pharmacy Sales
- Prescription-based dispensing.
- Direct medicine sales where permitted.
- Patient identification.
- Batch selection.
- Discounts, payment, invoice, and returns.

### Business Rules
1. Maintain batch-wise stock.
2. Never dispense expired medicine.
3. Track every stock movement.
4. Support FEFO (First Expiry, First Out).
5. Prevent unauthorized negative stock.
6. Maintain purchase and dispensing history.
7. Generate low-stock and expiry alerts.

## Module 09: Laboratory Information System (LIS)

- Test categories and test master.
- Test pricing, sample type, and reference ranges.
- Laboratory orders and sample collection.
- Sample tracking and processing.
- Result entry and verification.
- Report generation, printing, and historical results.

### Workflow
Doctor Order → Authorization / Billing → Sample Collection → Processing → Result Entry → Verification → Report Publication.

Requirements:
- Every order references a patient and encounter.
- Maintain sample identification and traceability.
- Separate result entry from verification.
- Highlight abnormal results according to configured reference ranges.
- Escalate critical results and record acknowledgement.
- Corrections to verified results must retain the original result and audit history.

## Module 10: Radiology & Imaging

- X-Ray, ultrasound, CT, MRI, ECG, echocardiogram, and configurable services.
- Imaging requests and scheduling.
- Technician and radiologist assignment.
- Procedure records.
- Image attachment and report templates.
- Report verification and publication.
- Previous imaging history.
- Future DICOM/PACS integration.

## Module 11: Operation Theatre (OT)

- OT and surgery master.
- Surgery scheduling.
- Surgeon, anesthesiologist, and nursing team assignment.
- Equipment requirements.
- Preoperative checklist.
- Operation and anesthesia notes.
- Postoperative notes and recovery management.
- OT utilization reports.

Workflow: Surgery Request → Approval → Schedule → Preoperative Assessment → OT Preparation → Surgery → Recovery → Completion.

## Module 12: Blood Bank

- Blood groups and donor registration.
- Donor history and blood collection.
- Blood component and unit identification.
- Stock and expiry tracking.
- Cross-matching and blood issue.
- Transfusion records and discard records.

Every blood unit must be traceable from collection through issue, transfusion, or disposal.

## Module 13: Bed, Ward & Cabin Management

- Buildings, floors, wards, rooms, cabins, and beds.
- Bed categories and pricing.
- Availability, reservation, allocation, and transfer.
- Cleaning and maintenance status.

Bed statuses: Available, Occupied, Reserved, Under Maintenance, Cleaning Required.

Prevent overlapping active assignments and double booking.

## Module 14: Employee & HR Management

### Employee Profile
- Employee ID and personal information.
- Contact and address.
- Department and designation.
- Joining date and employment type.
- Qualifications and professional registration.
- Salary and emergency contact.
- Employment status.

### HR Features
- Employee registration and assignment.
- Attendance and shift scheduling.
- Leave application and approval.
- Salary and payroll.
- Employee documents.
- Resignation and employment history.

### Doctor Compensation
Support configurable fixed salary, consultation commission, revenue sharing, per-visit, and per-procedure arrangements, subject to hospital policy.

## Module 15: Asset & Equipment Management

- Medical and non-medical asset categories.
- Asset code, name, manufacturer, model, serial number.
- Purchase date, price, supplier, warranty.
- Department and location assignment.
- Maintenance schedules and breakdown records.
- Repair history, warranty tracking, depreciation, and disposal.

Track the complete lifecycle of each asset.

## Module 16: Procurement & Store Management

- Supplier management.
- Purchase requisition and approval.
- Purchase order.
- Goods receipt and quality verification.
- Stock entry and transfer.
- Purchase return.
- Supplier invoice and payment.

Workflow: Requisition → Approval → Purchase Order → Goods Receipt → Inventory Update → Supplier Invoice → Payment.

Inventory categories may include medicines, medical consumables, laboratory and surgical supplies, stationery, cleaning materials, food supplies, and spare parts.

## Module 17: Billing & Patient Financial Management

### Billing Services
- OPD consultation.
- Emergency services.
- Admission and bed charges.
- Cabin, nursing, laboratory, radiology, surgery, pharmacy, and procedure charges.
- Other configurable hospital services.

### Features
- Service master and pricing.
- Patient invoice and invoice details.
- Discounts and approval.
- Advance and partial payment.
- Outstanding amount.
- Refund and payment allocation.
- Receipt generation.
- Future insurance and corporate billing.

### Payment Methods
- Cash.
- Bank.
- Card.
- Mobile Financial Services (bKash, Nagad, Rocket).
- Other configurable methods.

### Financial Rules
1. Every invoice has a unique number.
2. Every payment has a unique transaction reference.
3. Support partial payments and patient advances.
4. Maintain original invoice details.
5. Prevent unauthorized modification of finalized invoices.
6. Record refunds as separate financial transactions.
7. Apply discount approval limits.
8. Never delete finalized financial transactions.
9. Use cancellation and reversal workflows with authorization and audit records.

### Example
| Service | Amount |
|---|---:|
| Doctor Consultation | 1,000 BDT |
| CBC Test | 500 BDT |
| X-Ray | 800 BDT |
| Medicine | 1,200 BDT |
| **Total** | **3,500 BDT** |

If the patient pays 2,000 BDT, the system must show 1,500 BDT outstanding.

## Module 18: Accounting & Financial Management

- Chart of Accounts and account groups.
- Voucher management.
- Cash book and bank book.
- General Ledger.
- Trial Balance.
- Income Statement.
- Balance Sheet.
- Accounts Receivable and Payable.
- Income and expense management.
- Bank reconciliation.
- Financial period and closing management.

Every financial transaction must produce appropriate debit and credit entries.

Example: Patient pays 2,000 BDT in cash:
- Debit: Cash Account — 2,000 BDT.
- Credit: Patient Receivable — 2,000 BDT.

Maintain double-entry accounting, approval, posting, reversal, audit, and cashier reconciliation controls.

## Module 19: Ambulance & Transportation

- Ambulance and vehicle information.
- Driver information and assignment.
- Availability and emergency requests.
- Trip management and history.
- Fuel expenses and maintenance.

## Module 20: Reports & Management Dashboard

### Executive Dashboard
- Total registered patients.
- Today's patients and OPD visits.
- Current admitted patients.
- Available and occupied beds.
- Today's appointments.
- Pending laboratory reports.
- Today's revenue and outstanding balances.
- Today's expenses.
- Pharmacy stock alerts.
- Upcoming equipment maintenance.

### Patient Reports
- Registration, visit, admission, and discharge.
- Department-wise and doctor-wise patient reports.

### Clinical Reports
- Diagnosis, prescription, laboratory, radiology, surgery, and patient outcome reports.

### Financial Reports
- Daily and monthly collection.
- Service-wise and department-wise revenue.
- Doctor-wise revenue.
- Expenses, receivables, refunds, discounts.
- Profit and Loss, Balance Sheet, and Trial Balance.

### Inventory Reports
- Current stock and stock movement.
- Purchase and medicine sales.
- Expired and near-expiry medicines.
- Low stock and stock valuation.

### HR Reports
- Employee list, attendance, leave, payroll, and schedules.

Reports must support appropriate filters, date ranges, pagination, and Excel/PDF export. KPI definitions must be documented to prevent inconsistent calculations.

---

# 5. Cross-Module Business Workflows

## 5.1 OPD
Patient Registration → Appointment → Queue → Doctor Consultation → Diagnosis → Prescription → Investigation Request → Billing → Payment → Visit Completion.

## 5.2 IPD
Patient Registration → Admission → Bed Allocation → Initial Assessment → Doctor Treatment → Nursing Care → Investigation → Pharmacy → Daily Charges → Discharge Summary → Final Billing → Payment → Discharge.

## 5.3 Laboratory
Doctor Order → Authorization → Sample Collection → Processing → Result Entry → Verification → Report Publication.

## 5.4 Pharmacy
Prescription → Pharmacy Queue → Medicine Availability → Batch Selection → Dispensing → Stock Deduction → Invoice → Payment.

## 5.5 Procurement
Department Requisition → Approval → Purchase Order → Goods Receipt → Inventory Update → Supplier Invoice → Payment → Accounting Entry.

## 5.6 Asset
Asset Purchase → Asset Registration → Department Assignment → Maintenance Scheduling → Repair History → Depreciation → Disposal.

---

# 6. Common System Requirements

## 6.1 Notifications
Support in-app notifications for appointments, admissions, discharges, laboratory results, medicine expiry, low stock, pending approvals, and equipment maintenance. Future channels may include SMS, email, WhatsApp, and mobile push notifications.

## 6.2 Document Management
Securely store patient reports, prescriptions, discharge summaries, employee documents, purchase invoices, asset documents, and other hospital files. Apply access controls, retention policies, and document history.

## 6.3 Digital Approval
Support authorized approval workflows for prescriptions, laboratory reports, discharge summaries, refunds, discounts, purchase orders, and financial vouchers.

## 6.4 Audit Trail
Record user, action, module, entity, record ID, timestamp, relevant previous/new values, trace ID, and appropriate request metadata. Clinical and financial records require particularly strong auditability.

---

# 7. Non-Functional Requirements

## 7.1 Security
- Secure authentication and authorization.
- Role-, permission-, and data-level access control.
- HTTPS.
- Secure session and token management.
- Protection against common web vulnerabilities.
- Sensitive data protection.
- Secure file access.
- Audit logging.
- Backup and recovery procedures.

## 7.2 Performance
- Common pages should generally load within 2–3 seconds under expected load.
- Patient search must be efficient.
- Reports must use pagination and optimized queries.
- Long-running operations should be asynchronous where appropriate.
- Dashboard queries must not block transactional operations.
- Validate targets through realistic load testing.

## 7.3 Availability and Recovery
- Reliable hosting and monitoring.
- Automated backups.
- Tested database recovery.
- Defined Recovery Point Objective (RPO) and Recovery Time Objective (RTO).
- Documented downtime procedure for power, network, or server outages.

## 7.4 Scalability and Usability
- Support increasing patient and employee volumes.
- Allow additional departments and branches in the future.
- Responsive desktop, tablet, and mobile web interface.
- Simple navigation and fast data entry.
- Bangla and English localization where required.

---

# 8. Proposed Technical Direction

The detailed implementation standards are defined in `TECHNICAL-ARCHITECTURE.md`.

Initial direction:
- Modular Monolith.
- ASP.NET Core REST API.
- Clean Architecture.
- Angular frontend.
- PostgreSQL.
- Entity Framework Core and Npgsql.
- FluentValidation.
- Mapster.
- Serilog.
- JWT and RBAC.
- Docker.
- xUnit and integration testing.

Start with a modular monolith rather than microservices. Keep module boundaries explicit and use module-owned schemas and contracts.

Healthcare interoperability should be considered for future integration. Review the Bangladesh DGHS Core FHIR Implementation Guide: https://fhir.dghs.gov.bd/core/. Do not implement the entire FHIR specification in the first release.

---

# 9. Initial Database Domain Map

This is a logical starting point, not the final physical schema.

| Domain | Main Entities |
|---|---|
| Identity | Users, Roles, Permissions, UserRoles |
| Organization | Departments, Designations, Locations |
| Patient | Patients, PatientContacts, PatientDocuments, Allergies |
| Medical | MedicalHistories, Diagnoses, Encounters |
| Appointment | Appointments, DoctorSchedules, Queues |
| Clinical | ClinicalNotes, Prescriptions, PrescriptionDetails |
| IPD | Admissions, BedAssignments, Discharges |
| Nursing | NursingNotes, VitalSigns, MedicationAdministrations |
| Laboratory | LabOrders, LabOrderDetails, Samples, LabResults |
| Radiology | ImagingOrders, ImagingReports |
| Pharmacy | Medicines, Batches, PharmacySales, DispensingDetails |
| Inventory | Items, Warehouses, StockMovements |
| Procurement | Suppliers, PurchaseOrders, GoodsReceipts |
| Billing | Invoices, InvoiceDetails, Payments, Refunds |
| Finance | ChartOfAccounts, Vouchers, VoucherDetails |
| HR | Employees, Attendance, LeaveApplications, Payroll |
| Asset | Assets, AssetAssignments, MaintenanceRecords |
| OT | SurgerySchedules, SurgeryRecords, OTResources |
| Blood Bank | Donors, BloodUnits, CrossMatches, Transfusions |
| Audit | AuditLogs, ApprovalHistories |

The final database must be derived from validated workflows and business rules, not merely from this entity list.

---

# 10. Paperless Hospital Strategy

The goal is to digitize complete workflows, not merely replace paper forms with digital forms.

| Manual Process | Digital Process |
|---|---|
| Patient registration register | Digital patient registration |
| Appointment notebook | Online appointment scheduling |
| Paper prescription | Electronic prescription |
| Patient medical file | Electronic Medical Record |
| Bed register | Real-time bed dashboard |
| Laboratory register | Digital laboratory workflow |
| Printed diagnostic report | Digital diagnostic report |
| Medicine stock register | Batch-wise inventory |
| Equipment register | Digital asset management |
| Employee attendance register | Digital attendance |
| Manual invoice book | Electronic billing |
| Cash collection register | Digital cashier management |
| Paper approval | Digital approval workflow |

Generate printable PDFs when necessary, preserve document history, support authorized approvals, and minimize printing while retaining legally or operationally required physical documentation.

Paperless must not mean that patient care becomes impossible when the application is unavailable. Establish an approved downtime and recovery procedure.

---

# 11. Security, Privacy & Clinical Safety

- Doctors should access relevant patient records.
- Nurses should access assigned clinical workflows.
- Pharmacists should access prescription and dispensing information.
- Finance staff should access financial information according to permissions.
- Administrative visibility must not automatically grant unrestricted clinical access.
- Encrypt sensitive information in transit and protect it at rest.
- Record access to sensitive patient records.
- Maintain retention and deletion policies.
- Establish incident response procedures.
- The system must not automatically make clinical decisions.
- Diagnosis and treatment decisions remain with qualified healthcare professionals.
- Prescription changes must be traceable.
- Critical laboratory results need escalation and acknowledgement.
- Allergy information must be visible in relevant clinical workflows.

Review applicable Bangladesh healthcare, data protection, professional licensing, financial, and medical-record requirements with qualified local professionals before production deployment.

---

# 12. Key Performance Indicators (KPIs)

### Hospital Operations
- Daily and monthly patient count.
- OPD, IPD, and emergency patient count.
- Average waiting time.
- Bed occupancy percentage.
- Average length of stay.

### Financial Performance
- Daily and monthly revenue.
- Department- and service-wise revenue.
- Expenses.
- Outstanding receivables.
- Refunds.
- Operating margin.

### Medical Services
- Doctor-wise consultation count.
- Department-wise patient count.
- Laboratory and radiology volume.
- Surgery count.
- Follow-up rate.

### Inventory
- Inventory value.
- Expired and near-expiry medicine value.
- Low-stock items.
- Medicine consumption.

Define each KPI consistently and document its calculation.

---

# 13. Assumptions & Business Discovery Questions

Validate these before finalizing scope:

1. Is the hospital a single facility or multiple branches?
2. How many beds are available?
3. How many doctors and nurses work there?
4. Which departments currently operate?
5. Does the hospital have its own laboratory?
6. Does it have its own pharmacy?
7. Does it provide radiology services?
8. Does it have an operation theatre?
9. Does it operate an emergency department?
10. Does it have a blood bank?
11. Is integrated accounting required?
12. Does it currently use accounting or hospital software?
13. Are SMS notifications required?
14. Is patient self-service or online appointment booking required?
15. Is a separate doctor/nurse mobile application required?

Do not implement modules that the hospital does not operate unless they are explicitly planned for a later phase.

---

# 14. Initial Release Scope and Out of Scope

Recommended initial operational foundation:
- Patient Management.
- Doctor Management.
- Appointment.
- OPD.
- Billing.
- IPD.
- Bed Management.
- User and Permission Management.

Subsequent modules may include Pharmacy, Laboratory, Nursing, Inventory, HR, Assets, Accounting, Emergency, OT, and other advanced operations.

Do not include in the initial release unless approved:
- AI-based diagnosis.
- AI-generated prescriptions.
- Advanced medical decision support.
- Full telemedicine platform.
- Patient mobile applications.
- Insurance claim automation.
- Advanced multi-branch consolidation.
- National health information exchange integration.
- Advanced PACS infrastructure.

The architecture should allow future integration without implementing these capabilities prematurely.

---

# 15. Product Acceptance Criteria

The initial production release must satisfy its approved scope, including:

1. Secure user authentication.
2. Correct role and permission enforcement.
3. Patient registration and search.
4. Duplicate patient control.
5. Doctor and schedule management.
6. Appointment creation and tracking.
7. Digital OPD consultation.
8. Prescription creation and retrieval.
9. Accurate patient billing and payment.
10. Auditable financial transactions.
11. Appropriate patient-history access.
12. Transaction-safe critical workflows.
13. Reconciled reports.
14. Successful backup restoration test.
15. Security and performance tests.
16. Hospital User Acceptance Testing (UAT).
17. Documented downtime and recovery procedures.

---

# 16. Development Roadmap

## Phase 0: Business Discovery & System Foundation
- Confirm hospital structure, departments, roles, and workflows.
- Review current forms and registers.
- Finalize requirements and architecture.
- Prepare ERD, permission matrix, and design system.
- Establish development environment and authentication.

**Deliverables:** Approved requirements, architecture, initial ERD, project structure, login, user and role management.

## Phase 1: Administration & Patient Management
- Hospital configuration.
- Department management.
- Basic employee information.
- Patient registration, search, profile, ID, and documents.

**Deliverable:** Centralized patient registration.

## Phase 2: Appointment & OPD
- Doctor profiles and schedules.
- Appointment booking and queue.
- OPD visit and consultation.
- Diagnosis, prescription, and follow-up.

**Deliverable:** Outpatient workflow.

## Phase 3: Billing & Payments
- Service master and pricing.
- Invoices, payments, partial payments, advances.
- Discounts, refunds, cashier, and collection reports.

**Deliverable:** Integrated patient billing.

## Phase 4: IPD & Nursing
- Ward, room, and bed management.
- Admission and bed allocation.
- Patient monitoring, nursing, vital signs, and medication administration.
- Discharge.

**Deliverable:** Inpatient workflow.

## Phase 5: Laboratory & Radiology
- Test master, orders, samples, results, verification, reports.
- Imaging orders and reports.

**Deliverable:** Diagnostic management.

## Phase 6: Pharmacy & Inventory
- Medicine master, batches, expiry, dispensing, stock movements, suppliers, and purchasing.

**Deliverable:** Pharmacy and inventory.

## Phase 7: HR & Asset
- Employee, attendance, leave, shift, payroll.
- Asset registration, assignment, and maintenance.

**Deliverable:** Workforce and asset management.

## Phase 8: Advanced Hospital Operations
- Emergency, OT, blood bank, ambulance, insurance, and corporate patients as required.

**Deliverable:** Advanced hospital operations.

## Phase 9: Accounting & Advanced Reporting
- Chart of Accounts, vouchers, ledger, trial balance, income statement, balance sheet, cash/bank, reconciliation, and dashboards.

**Deliverable:** Integrated financial management.

## Phase 10: Production Readiness
- Security and performance testing.
- Backup and restore testing.
- Data migration.
- UAT and staff training.
- Production deployment and monitoring.

**Deliverable:** Production-ready HMIS.

---

# 17. Instructions for Claude Code Agent

Act as a Senior Business Analyst, Healthcare Software Architect, Database Architect, Backend Developer, Frontend Developer, and QA Engineer.

Do not immediately generate the entire application.

## First: Understand the Business
1. Read the complete BRD.
2. Understand all business modules and their relationships.
3. Identify dependencies and workflow conflicts.
4. Identify missing business rules and data-consistency requirements.
5. Identify security and clinical-safety concerns.

## Second: Perform Requirement Analysis
For every module, document:
- Business objective.
- Actors and responsibilities.
- Functional requirements.
- Workflows and business rules.
- Input and output fields.
- Validation and permission rules.
- Dependencies and exception scenarios.
- Acceptance criteria.

Ask for clarification instead of inventing ambiguous business rules.

## Third: Prepare Technical Documentation
Prepare:
1. System Architecture Document.
2. Module Dependency Diagram.
3. Database ERD and table specifications.
4. API endpoint specifications.
5. Role and Permission Matrix.
6. UI/UX navigation structure.
7. Workflow diagrams.
8. Integration specifications.
9. Testing strategy.

## Fourth: Development Rules
- Use modular architecture.
- Separate business logic from controllers.
- Follow SOLID principles.
- Use meaningful naming.
- Maintain consistent API responses.
- Implement server-side validation.
- Use transactions for critical operations.
- Implement error handling and audit trails.
- Avoid duplicated logic and unnecessary abstractions.
- Maintain tests and documentation.

## Fifth: Implement One Phase at a Time
For each phase:
1. Review requirements.
2. Identify entities and dependencies.
3. Design database changes.
4. Implement backend.
5. Implement frontend.
6. Integrate workflows.
7. Write and run tests.
8. Validate business rules.
9. Update documentation.
10. Verify existing modules are not broken.

Do not start the next phase until the current phase passes its acceptance criteria.

## Sixth: Maintain Documentation
Maintain:
- `/docs/BRD.md`
- `/docs/SRS.md`
- `/docs/ARCHITECTURE.md`
- `/docs/DATABASE.md`
- `/docs/API-SPECIFICATION.md`
- `/docs/WORKFLOWS.md`
- `/docs/ROLE-PERMISSION-MATRIX.md`
- `/docs/DEVELOPMENT-ROADMAP.md`
- `/docs/TESTING.md`
- `/docs/CHANGELOG.md`
- `/docs/IMPLEMENTATION-STATUS.md`

For every module, record:
- Not Started.
- Analysis Completed.
- Design Completed.
- Backend Completed.
- Frontend Completed.
- Testing Completed.
- Approved.

Keep documentation synchronized with actual implementation.

## Seventh: Prevent Uncontrolled Development
- Do not skip business analysis.
- Do not invent missing requirements.
- Do not implement all modules simultaneously.
- Do not change approved business rules without approval.
- Do not introduce microservices prematurely.
- Do not over-engineer.
- Do not expose patient information through unauthorized APIs.
- Do not silently modify finalized clinical or financial records.
- Do not mark incomplete features as complete.

---

# 18. Initial Task for Claude Code

**Start with Phase 0 only.**

Do not generate the entire application.

1. Analyze this BRD.
2. Identify missing business information.
3. Prepare module dependency analysis.
4. Prepare initial system architecture.
5. Prepare hospital navigation/menu structure.
6. Prepare database domain model and initial ERD.
7. Prepare role-permission matrix.
8. Identify Phase 1 implementation scope.
9. Create project documentation.
10. Present the proposed architecture and Phase 1 plan for review.

Do not proceed with major implementation until business and architecture proposals are reviewed and approved.

**Final Product Objective:** A professional, secure, integrated, paperless Hospital Management Information System that manages the patient lifecycle and the hospital's clinical, operational, financial, and administrative activities.
