namespace Hmis.BuildingBlocks.Contracts.Billing;

// Contracts owned and implemented by the Billing module. Clinical, OPD, IPD and other modules push charges into
// Billing through IChargeService; Billing never calls them back (MODULE-DEPENDENCIES.md).

/// <summary>Where a charge came from. One invoice per source; posting the same source again returns that invoice.</summary>
public static class ChargeSources
{
    public const string OpdAppointment = "OpdAppointment";
    public const string IpdAdmission = "IpdAdmission";
    public const string PatientRegistration = "PatientRegistration";
}

/// <summary>Price-list services that other modules charge automatically. Seeded by Billing with price 0 (no charge).</summary>
public static class StandardServiceCodes
{
    public const string Registration = "REG";
    public const string Admission = "ADM";
}

/// <param name="Category">A <see cref="ChargeCategories"/> value.</param>
public sealed record ServicePrice(string Code, string Name, string Category, decimal Price);

public interface IServicePriceLookup
{
    /// <summary>The current price of an active price-list service, or null when it does not exist or is inactive.</summary>
    Task<ServicePrice?> GetActiveAsync(string code, CancellationToken cancellationToken);
}

/// <summary>Service categories understood by Billing (unknown values are billed as "Other").</summary>
public static class ChargeCategories
{
    public const string Registration = "Registration";
    public const string Consultation = "Consultation";
    public const string Procedure = "Procedure";
    public const string Laboratory = "Laboratory";
    public const string Radiology = "Radiology";
    public const string Bed = "Bed";
    public const string Nursing = "Nursing";
    public const string Pharmacy = "Pharmacy";
    public const string Other = "Other";
}

public sealed record ChargeLine(string Description, string Category, int Quantity, decimal UnitPrice);

/// <param name="SourceReference">Human-readable reference printed on the invoice (e.g. the appointment number).</param>
public sealed record ChargeRequest(
    string SourceType,
    Guid SourceId,
    string SourceReference,
    Guid PatientId,
    IReadOnlyList<ChargeLine> Lines);

/// <param name="Status">Finalized or Cancelled.</param>
public sealed record ChargeResult(Guid InvoiceId, string InvoiceNumber, decimal NetAmount, decimal PaidAmount, string Status);

/// <param name="Key">Unique within the bill (e.g. "BED:2026-10-31"); a line whose key is already on the bill is skipped.</param>
public sealed record RunningChargeLine(string Key, string Description, string Category, int Quantity, decimal UnitPrice);

public sealed record RunningChargeRequest(
    string SourceType,
    Guid SourceId,
    string SourceReference,
    Guid PatientId,
    IReadOnlyList<RunningChargeLine> Lines);

/// <param name="Status">Draft (still collecting charges), Finalized or Cancelled.</param>
public sealed record BillStatus(
    Guid InvoiceId,
    string InvoiceNumber,
    string Status,
    decimal Subtotal,
    decimal NetAmount,
    decimal PaidAmount,
    decimal DueAmount,
    int LineCount);

public sealed record PatientBalance(decimal OutstandingAmount, decimal AdvanceBalance);

/// <summary>Amount due and advance deposit of a patient (e.g. to show the admission deposit status).</summary>
public interface IPatientBalanceLookup
{
    Task<PatientBalance> GetAsync(Guid patientId, CancellationToken cancellationToken);
}

public interface IChargeService
{
    /// <summary>
    /// Creates a finalized invoice for the source. Idempotent: when the source already has an invoice, that invoice is
    /// returned unchanged, so callers may post before saving their own state and safely retry.
    /// </summary>
    Task<ChargeResult> PostAsync(ChargeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the source's invoice when nothing was paid in cash (advance applied to it is released). A paid invoice
    /// is left as it is for the cashier to refund. Returns null when the source has no invoice.
    /// </summary>
    Task<ChargeResult?> CancelAsync(string sourceType, Guid sourceId, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Adds charges to the source's running bill (a draft invoice, created on first use). Idempotent per line key, so
    /// periodic posting (e.g. daily bed rent) can safely run again. Fails once the bill is finalized.
    /// </summary>
    Task<BillStatus> AddToRunningBillAsync(RunningChargeRequest request, CancellationToken cancellationToken);

    /// <summary>Finalizes the running bill so it can be paid in full (no-op when already finalized).</summary>
    Task<BillStatus> FinalizeRunningBillAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken);

    Task<BillStatus?> GetBillAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken);
}
