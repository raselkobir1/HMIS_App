using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Administration.Domain.Entities;

/// <summary>Global operating settings. Exactly one row.</summary>
[Audited]
public sealed class SystemSettings : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private SystemSettings() { }

    /// <summary>
    /// Hospital timezone (stored times are UTC). Fixed to Asia/Dhaka for this single-facility deployment;
    /// date logic (IDateTimeProvider) uses the same zone, so it is shown but not editable.
    /// </summary>
    public string TimeZone { get; private set; } = "Asia/Dhaka";

    public string CurrencyCode { get; private set; } = "BDT";
    public string CurrencySymbol { get; private set; } = "৳";

    /// <summary>Month (1–12) in which the financial year starts. Bangladesh fiscal year: July (7).</summary>
    public int FinancialYearStartMonth { get; private set; } = 7;

    public uint RowVersion { get; private set; }

    public static SystemSettings CreateDefault() => new();

    public void Update(string currencyCode, string currencySymbol, int financialYearStartMonth)
    {
        if (financialYearStartMonth is < 1 or > 12)
        {
            throw new DomainException("INVALID_FINANCIAL_YEAR_START", "Financial year start month must be between 1 and 12.");
        }

        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        CurrencySymbol = currencySymbol.Trim();
        FinancialYearStartMonth = financialYearStartMonth;
    }
}
