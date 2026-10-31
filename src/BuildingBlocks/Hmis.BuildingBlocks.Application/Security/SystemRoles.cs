namespace Hmis.BuildingBlocks.Application.Security;

/// <summary>Default roles seeded by the Identity module (docs/ROLE-PERMISSION-MATRIX.md §1).</summary>
public static class SystemRoles
{
    public const string SuperAdministrator = "Super Administrator";
    public const string HospitalAdministrator = "Hospital Administrator";
    public const string Receptionist = "Receptionist";
    public const string Doctor = "Doctor";
    public const string Nurse = "Nurse";
    public const string Pharmacist = "Pharmacist";
    public const string LaboratoryTechnician = "Laboratory Technician";
    public const string Radiologist = "Radiologist";
    public const string Accountant = "Accountant / Cashier";
    public const string HrManager = "HR Manager";
    public const string StoreManager = "Store Manager";
    public const string HospitalOwner = "Hospital Owner / Management";

    public static readonly IReadOnlyList<(string Name, string Description)> All =
    [
        (SuperAdministrator, "System-wide configuration, users, roles and permissions"),
        (HospitalAdministrator, "Hospital operations and departments"),
        (Receptionist, "Patient registration, appointments and queues"),
        (Doctor, "Consultations, diagnosis and prescriptions"),
        (Nurse, "Nursing care, vital signs and medication administration"),
        (Pharmacist, "Medicine inventory and dispensing"),
        (LaboratoryTechnician, "Sample processing and result entry"),
        (Radiologist, "Imaging findings and reports"),
        (Accountant, "Billing, collections and financial transactions"),
        (HrManager, "Employees, attendance, leave and payroll"),
        (StoreManager, "Inventory, purchasing and suppliers"),
        (HospitalOwner, "Read-only performance dashboards and reports")
    ];
}
