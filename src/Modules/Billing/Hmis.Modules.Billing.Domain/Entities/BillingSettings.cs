using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Billing.Domain.Entities;

/// <summary>
/// Hospital billing settings (BRD Module 01: payment methods, approval limits). One row; defaults apply until it is saved.
/// </summary>
[Audited]
public sealed class BillingSettings : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    public const decimal DefaultDiscountApprovalPercent = 10m;

    private BillingSettings() { }

    /// <summary>Discounts up to this share of the subtotal apply at once; larger ones wait for approval.</summary>
    public decimal DiscountApprovalPercent { get; private set; } = DefaultDiscountApprovalPercent;

    /// <summary>Comma-separated payment methods switched off at the counter (cash and advance cannot be switched off).</summary>
    public string DisabledPaymentMethods { get; private set; } = string.Empty;

    public uint RowVersion { get; private set; }

    public static BillingSettings CreateDefault() => new();

    public IReadOnlySet<PaymentMethod> Disabled =>
        DisabledPaymentMethods.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => Enum.TryParse<PaymentMethod>(m, out var method) ? method : (PaymentMethod?)null)
            .OfType<PaymentMethod>()
            .ToHashSet();

    public bool IsEnabled(PaymentMethod method) => !Disabled.Contains(method);

    public void Update(decimal discountApprovalPercent, IReadOnlyCollection<PaymentMethod> disabledMethods)
    {
        if (discountApprovalPercent is < 0 or > 100)
        {
            throw new DomainException("BILLING_DISCOUNT_LIMIT", "The discount limit must be between 0 and 100 percent.");
        }

        if (disabledMethods.Contains(PaymentMethod.Cash) || disabledMethods.Contains(PaymentMethod.Advance))
        {
            throw new DomainException("BILLING_METHOD_REQUIRED", "Cash and advance deposits cannot be switched off.");
        }

        DiscountApprovalPercent = Math.Round(discountApprovalPercent, 2);
        DisabledPaymentMethods = string.Join(',', disabledMethods.Distinct().OrderBy(m => m));
    }
}
