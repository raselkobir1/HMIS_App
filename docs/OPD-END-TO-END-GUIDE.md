# OPD End-to-End Guide: From Setup to Completed Visit

This guide walks through one complete outpatient (OPD) cycle in HMIS, from a blank system to a patient
who has been registered, booked, checked in, billed, seen by the doctor, and given a printed prescription.
It is written as a hands-on test script. Every step names the role that performs it, the menu to open,
what to fill in, and what the system does behind the scenes.

The whole cycle looks like this:

```
Part A — one-time setup (Hospital Administrator / HR Manager)
  A1 Sign in as superadmin          A5 Create the doctor profile (fees)
  A2 Check departments/designations  A6 Create staff logins and link the doctor login
  A3 Create the doctor as employee   A7 Price list (registration fee, optional)
  A4 (same as A3, different staff)   A8 Doctor sessions (weekly chamber)

Part B — the daily patient cycle
  B1 Register patient      [Receptionist]        -> MR number, optional registration fee bill
  B2 Book appointment      [Receptionist]        -> Scheduled, serial number
  B3 Check in              [Receptionist]        -> Waiting, consultation fee invoice created
  B4 Collect payment       [Accountant/Cashier]  -> invoice Paid, receipt printed
  B5 Record vitals         [Nurse]               -> vitals attached to the visit (optional)
  B6 Call next / see now   [Doctor]              -> InConsultation
  B7 Write consultation    [Doctor]              -> draft saved
  B8 Finalize & complete   [Doctor]              -> Completed, record locked
  B9 Print prescription    [Doctor/Reception]
  B10 Review reports       [Everyone with access]-> patient visits, invoice, collections, audit
```

Appointment status flow:

```
Scheduled -> Confirmed -> Waiting -> InConsultation -> Completed
                |            |
                +-> NoShow   +-> Cancelled (unpaid bill cancelled too)
```

---

## Part A: One-time setup

### A1. Sign in as the super administrator

1. Open the UI. With Docker Compose it is `http://localhost:4380`; with `ng serve` it is `http://localhost:4300`.
2. Sign in as `superadmin` with the seed password (`HMIS_SEED_ADMIN_PASSWORD` in `.env`, or the
   `Identity:SeedAdmin:Password` user secret). You must change the password on first sign-in.

The super administrator holds every administrative permission, so you can do all of Part A with this
one login. It does **not** hold the clinical permissions (vitals, consultations); those are never
granted to the super administrator automatically. For Part B you therefore need real role logins,
and in any case one login per role shows exactly what each role can do.

### A2. Check departments and designations

Menu: **Administration > Departments** and **Administration > Designations**.

Both are seeded on first start. You need at least:

- One active clinical department, for example "Medicine" or "Cardiology". The doctor and the
  appointment booking screen are grouped by department.
- One designation in the **Doctor** category. Seeded ones are **Consultant**, **Medical Officer** and
  **Resident Doctor**. Only an employee with a Doctor-category designation can get a doctor profile.

Nothing to do here unless you want to add your own department or designation.

### A3. Create the doctor as an employee

Menu: **HR > Employees > New employee**.

| Field | Example |
|---|---|
| Full name | Dr. Karim Ahmed |
| Gender, Date of birth, Mobile, Email, Address | any valid values |
| Department | Medicine |
| Designation | **Consultant** (must be a Doctor-category designation) |
| Employment type | Permanent |
| Joining date | today or earlier |

Save. The system allocates an employee code such as `EMP000001`. The employee status is Active, which
counts as "current". Only current employees appear in the employee pickers later.

### A4. Create the other staff as employees (optional but recommended)

Repeat A3 for a receptionist, a nurse and a cashier, using the Receptionist, Staff Nurse and Accountant
designations. Linking logins to employee records is optional for these roles, but it keeps the audit
trail tidy. It is **mandatory for the doctor** (see A6).

### A5. Create the doctor profile

Menu: **HR > Doctors > New doctor profile**.

1. In **Employee**, type at least two characters of the doctor's name or employee code and pick the
   employee from the dropdown. The Save button stays disabled until an employee is selected.
2. Fill in:

| Field | Example |
|---|---|
| Specialization | Cardiology |
| BMDC registration no. | A-12345 (letters, digits, space, `-`, `/` only; must be unique) |
| Qualifications | MBBS, FCPS (Medicine) |
| New patient fee (৳) | 800 |
| Follow-up fee (৳) | 500 |
| Inpatient visit fee (৳) | 0 (used by IPD only) |

Save. The fees entered here are copied onto every appointment at booking time, so changing them later
does not change already-booked appointments.

If you get "This employee's designation is not in the Doctor category", go back to A3 and change the
employee's designation. If you get "This employee already has a doctor profile", edit the existing
profile from the list instead.

### A6. Create logins and link the doctor's login to the employee

Menu: **Security > Users > New user**.

Create one user per role you want to test:

| Username | Roles | Link to employee |
|---|---|---|
| reception1 | Receptionist | optional |
| cashier1 | Accountant / Cashier | optional |
| nurse1 | Nurse | optional |
| drkarim | Doctor | **required**: pick Dr. Karim Ahmed in "Linked employee" |

Each user gets a temporary password and must change it at first sign-in.

The doctor link is what makes **My Queue** work. The server resolves the signed-in user to an employee,
then to a doctor profile, and only lets that doctor start or complete their own patients. A doctor
login without the link gets the error "Your login is not linked to a doctor profile".

The seeded roles already carry the permissions needed below. If you change them, the matrix is in
`docs/ROLE-PERMISSION-MATRIX.md`.

### A7. Price list and billing settings (optional)

Menu: **Billing > Price List** and **Billing > Billing Settings**.

- The consultation fee comes from the doctor profile, not from the price list, so no setup is needed
  for the basic cycle.
- To charge a **registration fee** for new patients, make sure the service with code **REG** exists,
  is active and has a price. It is billed once, automatically, when a patient is registered.
- **Billing Settings** sets which payment methods are enabled (Cash, Card, bKash, Nagad, Rocket, Bank
  transfer) and the discount percentage above which a discount needs administrator approval.

### A8. Create the doctor's weekly sessions

Menu: **Appointments > Doctor Sessions**. Select the doctor, then add a session.

| Field | Example |
|---|---|
| Day | Monday (add one row per weekday the doctor sits) |
| Room / chamber | Room 3 |
| Starts / Ends | 09:00 / 13:00 |
| Maximum patients | 30 |
| Minutes per patient | 10 |
| Effective from | today |
| Effective to | leave empty |

A session must exist for the weekday of the appointment date, otherwise the booking screen shows no
sessions for that doctor. To test today, make sure there is a session on today's weekday.

Leave is managed on the same page. A session on a leave day is not bookable.

---

## Part B: The daily patient cycle

Sign in as the role shown in each step. Open a second browser or a private window for each role so
you can keep them all signed in at once.

### B1. Register the patient (Receptionist)

Menu: **Patients > Register Patient**.

1. Enter full name, gender, date of birth (or age in years/months), and mobile number.
2. As you type, the form checks for possible duplicates on name, mobile and date of birth. If a match
   is shown, open the existing patient instead of creating a second record.
3. Fill in address, ID document, guardian and emergency contact as needed, then Save.

What the system does:

- Allocates an MR number (medical record number) from the numbering sequence.
- If the REG service is priced (A7), creates and finalizes a registration-fee invoice and shows the
  invoice number in the success message. Registration is never blocked by billing.
- Opens the patient profile. From here you can print the **ID card** or click **Book appointment**.

### B2. Book the appointment (Receptionist)

Menu: **Appointments > Book Appointment** (or the Book appointment button on the patient profile).

1. **Patient**: search by MR number, name or mobile and select the patient.
2. **Doctor and date**: choose the department, then the doctor. Choose **Appointment** and pick a date,
   or **Walk-in (today)** for a patient standing at the desk.
3. Click the session you want. The screen shows how many of the session's slots are already taken.
4. **Visit type**: New patient or Follow-up. This decides which doctor fee is snapshotted.
5. Optional notes, then **Book**.

What the system does:

- Creates the appointment in status **Scheduled** with an appointment number and a **serial number**,
  which is the patient's queue token for that session.
- Calculates the estimated time from the serial and minutes-per-patient.
- Stores the fee on the appointment. No invoice yet.

Optional: on **Appointments > Appointments** you can **Confirm** a scheduled booking (for example after
a reminder call). This only changes the status to Confirmed and is not required.

### B3. Check in on the day (Receptionist)

Menu: **Appointments > Appointments**. Filter by today's date and the doctor.

When the patient arrives, click **Check in** on their row.

What the system does:

- Moves the appointment to **Waiting** and puts it in the doctor's queue in serial order.
- Posts the consultation fee to Billing: a finalized, unpaid invoice with one line
  "Consultation fee — Dr. Karim Ahmed (new patient)". The invoice carries the appointment number.
- Shows the patient on the **Queue Display** board (serial numbers only, no names) and on the nurse's
  **OPD Vitals** list.

Rules:

- Check-in is only allowed on the appointment date.
- If the patient does not arrive, click **Mark as no-show** at the end of the session. That closes the
  appointment as **NoShow** and creates no bill.
- If the patient cancels, click **Cancel**. The unpaid invoice is cancelled as well; a paid one is kept
  for the cashier to refund.

### B4. Collect the consultation fee (Accountant / Cashier)

Menu: **Billing > Invoices**. Filter by status **Unpaid** or search by the appointment number.
You can also reach the patient's bills from the patient profile via the **Billing** button.

1. Open the invoice.
2. Click **Receive payment**. Choose the method (Cash, bKash, etc.), the amount (full or partial), a
   transaction or slip reference for non-cash methods, and optional notes. Save.
3. Click **Print** on the receipt that is created, or **Print** on the invoice itself.

If a discount is needed, click **Discount** first. Within the limit from Billing Settings it applies at
once; above it, it waits in **Billing > Approvals** for a Hospital Administrator.

Payment is **never a precondition** for the consultation. The doctor can see the patient whether or
not the invoice is paid; the invoice simply stays Unpaid on the patient's account.

### B5. Record vitals while waiting (Nurse, optional)

Menu: **OPD > OPD Vitals**. Select the doctor to see the checked-in patients.

Click **Vitals** on the patient's row and enter temperature, pulse, blood pressure, weight and so on.
The readings are attached to the visit and appear in the doctor's consultation screen.

### B6. Call the patient (Doctor)

Sign in as the doctor login that was linked in A6. Menu: **Appointments > My Queue**.

The page shows the current patient, the waiting list in serial order, and the completed list.

- Click **Call next** to take the first waiting patient, or **See now** on a specific patient.
- The appointment moves to **InConsultation** and the consultation screen opens.
- Only one patient can be in consultation per doctor. Finish the current one before calling the next.

### B7. Write the consultation (Doctor)

The consultation screen (`/opd/encounters/<appointment id>`) has these sections:

| Section | What to enter |
|---|---|
| History | Chief complaint, history of present illness, examination findings |
| Vitals | Already there if the nurse recorded them; the doctor can add a reading too |
| Diagnoses | One or more; mark one as **Primary** |
| Prescription | Medicine, form, strength, dose, when, duration, route, quantity, instructions. Rows can be reordered |
| Investigations | Tests to order, with notes (printed on the prescription; the Lab module comes in a later phase) |
| Advice & follow-up | Advice (English or Bangla), follow-up date and instructions, referral and reason |

Click **Save draft** as often as you like. A draft is editable until it is finalized.

### B8. Finalize and complete (Doctor)

Click **Finalize & complete** and confirm.

What the system does:

- Locks the consultation. Later changes can only be added as **addenda**, which are shown separately
  with their own timestamp.
- Sets the appointment to **Completed** and removes it from the queue.
- The doctor is returned to **My Queue** to call the next patient.

A doctor who does not chart in the system can instead press **Complete** directly in My Queue; the
appointment is completed without a consultation record.

### B9. Print the prescription

On the finalized consultation click **Print prescription**. The print view
(`/opd/consultations/<id>/print`) carries the hospital letterhead, patient details, diagnoses,
medicines, investigations, advice and follow-up date. Reception can open the same page from the
patient's **Visits** tab.

### B10. Where to see the results (reports)

| What | Where |
|---|---|
| Patient's visit history, diagnosis, medicines, status | **Patients > Find Patient** > open patient > **Visits** tab. Each row opens the read-only consultation |
| Patient's appointments | Same profile > **Appointments** tab |
| Patient's invoices, payments, advance balance, refunds | Patient profile > **Billing** button (Billing > patient account) |
| Today's appointments by status, doctor, date | **Appointments > Appointments** with filters |
| Invoice and receipt printouts | **Billing > Invoices** > open invoice > Print; receipt print from the payment |
| Daily cash collection by method and cashier | **Billing > Collections** (choose date range, click Show, then Print) |
| Pending discounts and refunds | **Billing > Approvals** |
| Counts for today (appointments, admissions, collections, etc.) | **Dashboard** |
| Who did what, when | **Security > Audit Log**; sign-ins under **Security > Login History** |

---

## Quick troubleshooting

| Symptom | Cause and fix |
|---|---|
| Doctor profile dialog: Save stays disabled | No employee selected. Employees list is empty, or the search term is under two characters. Create the employee first (A3) |
| "This employee's designation is not in the Doctor category" | Change the employee's designation to Consultant / Medical Officer / Resident Doctor |
| Booking screen shows no sessions | No session for that weekday, or the doctor is on leave that day, or the profile has "Accepting new appointments" unticked (A8, A5) |
| Check in button missing | Appointment date is not today, or the login lacks Appointment.CheckIn (Receptionist) |
| My Queue says the login is not linked to a doctor profile | Edit the user under Security > Users and pick the doctor's employee record (A6) |
| "Complete the current consultation before calling the next patient" | Finalize or Complete the patient already InConsultation |
| No invoice after check-in | The appointment fee was 0 (doctor fees not set at the time of booking) |
| Receive payment button missing | Login lacks Billing.Payment.Collect (Accountant / Cashier role) |

## Reference: who can do what in this cycle

| Action | Permission | Seeded roles |
|---|---|---|
| Register / edit patients | Patient.Create, Patient.Update | Receptionist |
| Book, confirm, reschedule | Appointment.Book | Receptionist |
| Check in, no-show | Appointment.CheckIn | Receptionist |
| Cancel appointment | Appointment.Cancel | Receptionist |
| Run own queue, start, complete | Appointment.Consult | Doctor |
| Write and finalize consultation | Clinical.Consultation.Manage | Doctor |
| Record vitals | Clinical.Vitals.Record | Nurse, Doctor |
| Receive payment, give discount, request refund | Billing.Payment.Collect, Billing.Discount.Apply, Billing.Refund.Request | Accountant / Cashier |
| Approve discounts and refunds, cancel finalized invoices | Billing.Discount.Approve, Billing.Refund.Approve, Billing.Invoice.Cancel | Hospital Administrator |
| Manage employees and doctor profiles | HR.Employees.Manage, HR.Doctors.Manage | Hospital Administrator, HR Manager |
| Manage doctor sessions and leave | Appointment.Schedules.Manage | Hospital Administrator |
| Create users and link employees | Identity.Users.Manage | Hospital Administrator |
