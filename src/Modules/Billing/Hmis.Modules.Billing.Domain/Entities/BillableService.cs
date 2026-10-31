using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Billing.Domain.Entities;

public enum ServiceCategory
{
    Registration,
    Consultation,
    Procedure,
    Laboratory,
    Radiology,
    Bed,
    Nursing,
    Pharmacy,
    Other
}

/// <summary>
/// An entry in the service price list (BRD Module 17: service master and pricing). Invoice lines copy the code, name
/// and price at the time of billing, so later price changes never alter existing invoices.
/// </summary>
[Audited]
public sealed class BillableService : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private BillableService() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public ServiceCategory Category { get; private set; }
    public decimal Price { get; private set; }

    /// <summary>The cashier may change the price on the invoice (e.g. "Miscellaneous charge").</summary>
    public bool IsPriceEditable { get; private set; }

    public bool IsActive { get; private set; } = true;
    public uint RowVersion { get; private set; }

    public static BillableService Create(string code, string name, ServiceCategory category, decimal price, bool isPriceEditable)
    {
        var service = new BillableService { Code = code.Trim().ToUpperInvariant() };
        service.Update(name, category, price, isPriceEditable);
        return service;
    }

    public void Update(string name, ServiceCategory category, decimal price, bool isPriceEditable)
    {
        if (price < 0)
        {
            throw new DomainException("SERVICE_PRICE_INVALID", "The price cannot be negative.");
        }

        Name = name.Trim();
        Category = category;
        Price = Money.Round(price);
        IsPriceEditable = isPriceEditable;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}

public static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
