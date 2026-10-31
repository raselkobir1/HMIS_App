using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Billing.Domain.Entities;

namespace Hmis.Modules.Billing.Application;

/// <summary>
/// Invoices and discounts, plus <see cref="IChargeService"/> for charges posted by other modules. Every change to a
/// finalized invoice runs under the patient's billing lock (see <see cref="IInvoiceRepository.LockPatientAsync"/>).
/// </summary>
public sealed class InvoiceManager(
    IInvoiceRepository invoices,
    IBillableServiceRepository services,
    IBillingUnitOfWork unitOfWork,
    BillingDtoFactory dtos,
    IPatientLookup patientLookup,
    INumberSequenceService numberSequences,
    IPermissionChecker permissions,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IInvoiceManager, IChargeService
{
    // ---------- Queries ----------

    public async Task<PagedResponse<InvoiceSummaryDto>> SearchAsync(InvoiceListQuery query, CancellationToken cancellationToken)
    {
        var page = await invoices.SearchAsync(query, cancellationToken);
        var items = await dtos.SummariesAsync(page.Items, cancellationToken);
        var byId = items.ToDictionary(i => i.Id);
        return page.Map(i => byId[i.Id]);
    }

    public async Task<InvoiceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dtos.InvoiceAsync(await GetAsync(id, cancellationToken), cancellationToken);

    // ---------- Draft invoices ----------

    public async Task<InvoiceDto> CreateAsync(InvoiceCreateDto request, CancellationToken cancellationToken)
    {
        await EnsurePatientBillableAsync(request.PatientId, cancellationToken);
        var lines = await ToLineValuesAsync(request.Lines, cancellationToken);

        // Allocated outside the save (Administration's own atomic statement); a failed save leaves a gap, never a duplicate.
        var number = await numberSequences.NextAsync(BillingNumberSequences.Invoice, cancellationToken);
        var invoice = Invoice.CreateDraft(number, request.PatientId, dateTime.LocalToday, request.Notes);
        invoice.SetLines(lines);
        await invoices.AddAsync(invoice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.InvoiceAsync(invoice, cancellationToken);
    }

    public async Task<InvoiceDto> UpdateAsync(Guid id, InvoiceUpdateDto request, CancellationToken cancellationToken)
    {
        var invoice = await GetAsync(id, cancellationToken);
        invoices.SetExpectedRowVersion(invoice, request.RowVersion);
        var lines = await ToLineValuesAsync(request.Lines, cancellationToken);
        invoice.UpdateNotes(request.Notes);
        invoice.SetLines(lines);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.InvoiceAsync(invoice, cancellationToken);
    }

    public async Task<InvoiceDto> FinalizeAsync(Guid id, RowVersionDto request, CancellationToken cancellationToken)
    {
        var invoice = await GetAsync(id, cancellationToken);
        invoices.SetExpectedRowVersion(invoice, request.RowVersion);
        invoice.Finalize(dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.InvoiceAsync(invoice, cancellationToken);
    }

    /// <summary>Discarding a draft needs Billing.Invoice.Manage; cancelling a finalized invoice needs Billing.Invoice.Cancel.</summary>
    public async Task<InvoiceDto> CancelAsync(Guid id, InvoiceCancelDto request, CancellationToken cancellationToken)
    {
        var invoice = await LockedAsync(id, async (invoice, ct) =>
        {
            // An invoice that is already cancelled falls through to the domain, which reports that plainly.
            var required = invoice.Status == InvoiceStatus.Draft ? BillingPermissions.InvoiceManage : BillingPermissions.InvoiceCancel;
            if (invoice.Status != InvoiceStatus.Cancelled && !await HasPermissionAsync(required, ct))
            {
                throw new ForbiddenException(invoice.Status == InvoiceStatus.Draft
                    ? "You do not have permission to discard draft invoices."
                    : "Only an administrator can cancel a finalized invoice.");
            }

            invoice.Cancel(request.Reason, dateTime.UtcNow);
        }, cancellationToken);

        return await dtos.InvoiceAsync(invoice, cancellationToken);
    }

    // ---------- Discounts ----------

    /// <summary>
    /// Applies at once while the invoice's total discount stays within <see cref="BillingPolicy.DiscountApprovalPercent"/>
    /// of the subtotal (or the user may approve discounts); otherwise it waits for approval.
    /// </summary>
    public async Task<InvoiceDto> RequestDiscountAsync(Guid id, DiscountRequestDto request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var invoice = await LockedAsync(id, async (invoice, ct) =>
        {
            var amount = invoice.CalculateDiscount(request.Type, request.Value);
            var withinLimit = invoice.DiscountPercentWith(amount) <= BillingPolicy.DiscountApprovalPercent;
            var approvedBy = withinLimit || await HasPermissionAsync(BillingPermissions.DiscountApprove, ct) ? userId : (Guid?)null;
            invoice.RequestDiscount(request.Type, request.Value, request.Reason, approvedBy, dateTime.UtcNow);
        }, cancellationToken);

        return await dtos.InvoiceAsync(invoice, cancellationToken);
    }

    public Task<InvoiceDto> ApproveDiscountAsync(Guid discountId, DecisionDto request, CancellationToken cancellationToken) =>
        DecideDiscountAsync(discountId, (invoice, userId) => invoice.ApproveDiscount(discountId, userId, request.Note, dateTime.UtcNow),
            cancellationToken);

    public Task<InvoiceDto> RejectDiscountAsync(Guid discountId, DecisionDto request, CancellationToken cancellationToken) =>
        DecideDiscountAsync(discountId, (invoice, userId) => invoice.RejectDiscount(discountId, userId, request.Note, dateTime.UtcNow),
            cancellationToken);

    // ---------- IChargeService (charges posted by other modules) ----------

    async Task<ChargeResult> IChargeService.PostAsync(ChargeRequest request, CancellationToken cancellationToken)
    {
        if (await invoices.GetBySourceAsync(request.SourceType, request.SourceId, cancellationToken) is { } existing)
        {
            return ToResult(existing);
        }

        var lines = request.Lines.Select(l => new InvoiceLineValues(null, null, null, l.Description,
            Enum.TryParse<ServiceCategory>(l.Category, out var category) ? category : ServiceCategory.Other, l.Quantity, l.UnitPrice)).ToList();
        var number = await numberSequences.NextAsync(BillingNumberSequences.Invoice, cancellationToken);

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // The patient lock also serializes two posts of the same source; the unique source index backs it up.
            await invoices.LockPatientAsync(request.PatientId, ct);
            if (await invoices.GetBySourceAsync(request.SourceType, request.SourceId, ct) is { } raced)
            {
                return ToResult(raced);
            }

            var invoice = Invoice.CreateFromCharge(number, request.PatientId, dateTime.LocalToday, request.SourceType, request.SourceId,
                request.SourceReference, lines, dateTime.UtcNow);
            await invoices.AddAsync(invoice, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return ToResult(invoice);
        }, cancellationToken);
    }

    async Task<ChargeResult?> IChargeService.CancelAsync(string sourceType, Guid sourceId, string reason, CancellationToken cancellationToken)
    {
        var found = await invoices.GetBySourceAsync(sourceType, sourceId, cancellationToken);
        if (found is null)
        {
            return null;
        }

        var invoice = await LockedAsync(found.Id, (invoice, _) =>
        {
            if (invoice.Status != InvoiceStatus.Cancelled && invoice.CashHeldAmount <= 0 && !invoice.CancellationRequested)
            {
                invoice.Cancel(reason, dateTime.UtcNow);
            }

            return Task.CompletedTask;
        }, cancellationToken);

        return ToResult(invoice);
    }

    private static ChargeResult ToResult(Invoice i) => new(i.Id, i.InvoiceNumber, i.NetAmount, i.PaidAmount, i.Status.ToString());

    // ---------- helpers ----------

    private async Task<InvoiceDto> DecideDiscountAsync(Guid discountId, Action<Invoice, Guid> decide, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var found = await invoices.GetByDiscountIdAsync(discountId, cancellationToken) ?? throw new NotFoundException("Discount", discountId);
        var invoice = await LockedAsync(found.Id, (invoice, _) =>
        {
            decide(invoice, userId);
            return Task.CompletedTask;
        }, cancellationToken);

        return await dtos.InvoiceAsync(invoice, cancellationToken);
    }

    /// <summary>Loads the invoice under its patient's billing lock, applies the change and saves, in one transaction.</summary>
    private async Task<Invoice> LockedAsync(Guid id, Func<Invoice, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        // Read the patient without tracking, so the invoice is loaded fresh once the lock is held.
        var patientId = await invoices.GetPatientIdAsync(id, cancellationToken) ?? throw new NotFoundException("Invoice", id);
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await invoices.LockPatientAsync(patientId, ct);
            var invoice = await invoices.GetWithDetailsAsync(id, ct) ?? throw new NotFoundException("Invoice", id);
            await change(invoice, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return invoice;
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<InvoiceLineValues>> ToLineValuesAsync(IReadOnlyList<InvoiceLineInputDto> lines, CancellationToken cancellationToken)
    {
        var catalog = (await services.GetByIdsAsync(lines.Select(l => l.ServiceId).Distinct().ToList(), cancellationToken)).ToDictionary(s => s.Id);
        return lines.Select((l, index) =>
        {
            if (!catalog.TryGetValue(l.ServiceId, out var service) || !service.IsActive)
            {
                throw new BusinessException(BillingErrorCodes.ServiceUnavailable, "This service is not in the active price list.",
                    $"lines[{index}].serviceId");
            }

            var price = service.IsPriceEditable ? l.UnitPrice ?? service.Price : service.Price;
            return new InvoiceLineValues(l.Id, service.Id, service.Code, service.Name, service.Category, l.Quantity, price);
        }).ToList();
    }

    private async Task EnsurePatientBillableAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var patient = await patientLookup.GetAsync(patientId, cancellationToken);
        if (patient is not { Status: "Active" })
        {
            throw new BusinessException(BillingErrorCodes.PatientUnavailable,
                patient?.Status == "Merged" ? "This patient record was merged into another record. Bill the surviving record." : "Select an active patient.",
                "patientId");
        }
    }

    private Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken) =>
        permissions.HasPermissionAsync(currentUser.UserId ?? throw new UnauthorizedException(), permission, cancellationToken);

    private async Task<Invoice> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await invoices.GetWithDetailsAsync(id, cancellationToken) ?? throw new NotFoundException("Invoice", id);
}
