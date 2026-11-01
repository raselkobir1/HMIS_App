using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Billing.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hmis.Modules.Billing.Application;

public sealed record BillingSettingsDto(
    decimal DiscountApprovalPercent, IReadOnlyList<PaymentMethod> EnabledMethods, IReadOnlyList<PaymentMethod> AllMethods, uint RowVersion);

public sealed record BillingSettingsUpdateDto
{
    public decimal DiscountApprovalPercent { get; init; }
    public IReadOnlyList<PaymentMethod> EnabledMethods { get; init; } = [];
    public uint RowVersion { get; init; }
}

public interface IBillingSettingsRepository : IRepository<BillingSettings>, IConcurrencyRepository<BillingSettings>
{
    /// <summary>Tracked; null until the settings are first saved.</summary>
    Task<BillingSettings?> GetAsync(CancellationToken cancellationToken);
}

public interface IBillingSettingsManager
{
    Task<BillingSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task<BillingSettingsDto> UpdateAsync(BillingSettingsUpdateDto request, CancellationToken cancellationToken);
}

/// <summary>Discount approval limit and the payment methods offered at the counter.</summary>
public sealed class BillingSettingsManager(IBillingSettingsRepository settings, IBillingUnitOfWork unitOfWork) : IBillingSettingsManager
{
    public async Task<BillingSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await settings.GetAsync(cancellationToken) ?? BillingSettings.CreateDefault());

    public async Task<BillingSettingsDto> UpdateAsync(BillingSettingsUpdateDto request, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (current is null)
        {
            current = BillingSettings.CreateDefault();
            await settings.AddAsync(current, cancellationToken);
        }
        else
        {
            settings.SetExpectedRowVersion(current, request.RowVersion);
        }

        current.Update(request.DiscountApprovalPercent, Enum.GetValues<PaymentMethod>().Except(request.EnabledMethods).ToList());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(current);
    }

    /// <summary>Current settings for the counter rules (defaults when never saved).</summary>
    public static async Task<BillingSettings> CurrentAsync(IBillingSettingsRepository settings, CancellationToken cancellationToken) =>
        await settings.GetAsync(cancellationToken) ?? BillingSettings.CreateDefault();

    private static BillingSettingsDto ToDto(BillingSettings s) =>
        new(s.DiscountApprovalPercent, Enum.GetValues<PaymentMethod>().Where(s.IsEnabled).ToList(), Enum.GetValues<PaymentMethod>(), s.RowVersion);
}

/// <summary>Price-list lookup and the merge guard that Billing provides to other modules.</summary>
public sealed class BillingContractServices(
    IBillableServiceRepository services,
    IInvoiceRepository invoices,
    IPaymentRepository payments,
    IRefundRepository refunds) : IServicePriceLookup, IPatientMergeGuard
{
    public async Task<ServicePrice?> GetActiveAsync(string code, CancellationToken cancellationToken) =>
        await services.GetByCodeAsync(code, cancellationToken) is { IsActive: true } s
            ? new ServicePrice(s.Code, s.Name, s.Category.ToString(), s.Price)
            : null;

    /// <summary>Money must be settled on a duplicate record before it is merged: dues, deposits and open refunds.</summary>
    public async Task<IReadOnlyList<string>> GetMergeBlockersAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        if (await invoices.GetOutstandingAsync(patientId, cancellationToken) is > 0 and var due)
        {
            blockers.Add($"{due:0.00} due on bills");
        }

        if (await invoices.HasDraftAsync(patientId, cancellationToken))
        {
            blockers.Add("an unfinished (draft) bill");
        }

        if (await payments.GetAdvanceBalanceAsync(patientId, cancellationToken) is > 0 and var advance)
        {
            blockers.Add($"{advance:0.00} advance deposit (refund or use it first)");
        }

        if ((await refunds.GetForPatientAsync(patientId, 50, cancellationToken)).Any(r => r.Status is RefundStatus.Requested or RefundStatus.Approved))
        {
            blockers.Add("a refund not yet paid out");
        }

        return blockers;
    }
}

/// <summary>Seeds the price-list services other modules charge automatically, at price 0 (no charge until priced).</summary>
public sealed class BillingSeedTask(IBillableServiceRepository services, IBillingUnitOfWork unitOfWork, ILogger<BillingSeedTask> logger) : IStartupTask
{
    private static readonly (string Code, string Name, ServiceCategory Category)[] Standard =
    [
        (StandardServiceCodes.Registration, "Patient registration", ServiceCategory.Registration),
        (StandardServiceCodes.Admission, "Admission charge", ServiceCategory.Bed)
    ];

    public int Order => 50;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var added = 0;
        foreach (var (code, name, category) in Standard)
        {
            if (!await services.CodeExistsAsync(code, cancellationToken))
            {
                await services.AddAsync(BillableService.Create(code, name, category, 0, false), cancellationToken);
                added++;
            }
        }

        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} standard billing services (price 0: set a price to start charging)", added);
        }
    }
}
