namespace Hmis.BuildingBlocks.Application.Seeding;

/// <summary>
/// Demo staff and logins seeded at startup for development and training installs (configuration section "DemoData").
/// Off by default: a production install must never carry accounts with a published password. When enabled, HR seeds one
/// employee per role (with a doctor profile for the doctor) and Identity seeds a login for each, all idempotently.
/// </summary>
public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    public bool Enabled { get; set; }

    /// <summary>Password given to every demo login. Must satisfy the password policy (8+ chars, upper, lower, digit).</summary>
    public string Password { get; set; } = "Demo@2026";
}

/// <summary>
/// Well-known ids of the demo employees so Identity can link each demo login to its HR record without a cross-module
/// query. Identity stores the employee id only (no foreign key), so the two seeds do not depend on each other's order.
/// </summary>
public static class DemoDataIds
{
    public static readonly Guid DoctorEmployeeId = new("0000d3e0-0000-7000-8000-000000000001");
    public static readonly Guid ReceptionistEmployeeId = new("0000d3e0-0000-7000-8000-000000000002");
    public static readonly Guid NurseEmployeeId = new("0000d3e0-0000-7000-8000-000000000003");
    public static readonly Guid CashierEmployeeId = new("0000d3e0-0000-7000-8000-000000000004");
    public static readonly Guid AdministratorEmployeeId = new("0000d3e0-0000-7000-8000-000000000005");
}
