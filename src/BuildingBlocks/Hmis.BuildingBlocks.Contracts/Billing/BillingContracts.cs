namespace Hmis.BuildingBlocks.Contracts.Billing;

// Contracts owned and implemented by the Billing module. Clinical, OPD, IPD and other modules push charges into
// Billing through IChargeService; Billing never calls them back (MODULE-DEPENDENCIES.md).

/// <summary>Where a charge came from. One invoice per source; posting the same source again returns that invoice.</summary>
public static class ChargeSources
{
    public const string OpdAppointment = "OpdAppointment";
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
}
