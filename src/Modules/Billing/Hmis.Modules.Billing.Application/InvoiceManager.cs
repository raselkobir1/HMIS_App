using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Notifications;
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
    IDateTimeProvider dateTime,
    IBillingSettingsRepository settings,
    INotificationService notifications) : IInvoiceManager, IChargeService
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

        // On a running bill, charges posted by other modules stay as they are; the cashier edits only their own lines.
        var posted = invoice.Lines.Where(l => l.ChargeKey is not null).OrderBy(l => l.SortOrder).ToList();
        if (request.Lines.Any(l => l.Id is { } lineId && posted.Any(p => p.Id == lineId)))
        {
            throw new BusinessException(BillingErrorCodes.LineLocked, "Charges posted by the ward cannot be changed here.");
        }

        var lines = await ToLineValuesAsync(request.Lines, cancellationToken);
        invoice.UpdateNotes(request.Notes);
        invoice.SetLines(posted
            .Select(l => new InvoiceLineValues(l.Id, l.ServiceId, l.ServiceCode, l.Description, l.Category, l.Quantity, l.UnitPrice, l.ChargeKey))
            .Concat(lines)
            .ToList());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.InvoiceAsync(invoice, cancellationToken);
    }

    public async Task<InvoiceDto> FinalizeAsync(Guid id, RowVersionDto request, CancellationToken cancellationToken)
    {
        var invoice = await GetAsync(id, cancellationToken);
        EnsureNotRunningBill(invoice);
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
            EnsureNotRunningBill(invoice);

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
    /// Applies at once while the invoice's total discount stays within the configured approval limit (Billing settings) of the
    /// subtotal, or when the user may approve discounts; otherwise it waits for approval and approvers are notified.
    /// </summary>
    public async Task<InvoiceDto> RequestDiscountAsync(Guid id, DiscountRequestDto request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var limit = (await BillingSettingsManager.CurrentAsync(settings, cancellationToken)).DiscountApprovalPercent;
        var pending = false;
        var invoice = await LockedAsync(id, async (invoice, ct) =>
        {
            var amount = invoice.CalculateDiscount(request.Type, request.Value);
            var withinLimit = invoice.DiscountPercentWith(amount) <= limit;
            var approvedBy = withinLimit || await HasPermissionAsync(BillingPermissions.DiscountApprove, ct) ? userId : (Guid?)null;
            invoice.RequestDiscount(request.Type, request.Value, request.Reason, approvedBy, dateTime.UtcNow);
            pending = approvedBy is null;
        }, cancellationToken);

        if (pending)
        {
            await notifications.NotifyPermissionAsync(BillingPermissions.DiscountApprove, new NotificationMessage(NotificationTypes.DiscountApproval,
                $"Discount waiting for approval · {invoice.InvoiceNumber}", $"{request.Reason} (above the {limit:0.##}% limit)", "/billing/approvals"),
                cancellationToken);
        }

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
        if (await invoices.GetBySourceAsync(request.SourceType, request.SourceId, cancellationToken) is { Count: > 0 } existing)
        {
            return ToResult(existing[^1]);
        }

        var lines = request.Lines.Select(l => new InvoiceLineValues(null, null, null, l.Description, ToCategory(l.Category), l.Quantity, l.UnitPrice))
            .ToList();
        var number = await numberSequences.NextAsync(BillingNumberSequences.Invoice, cancellationToken);

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // The patient lock also serializes two posts of the same source; the unique source index backs it up.
            await invoices.LockPatientAsync(request.PatientId, ct);
            if (await invoices.GetBySourceAsync(request.SourceType, request.SourceId, ct) is { Count: > 0 } raced)
            {
                return ToResult(raced[^1]);
            }

            var invoice = Invoice.CreateFromCharge(number, request.PatientId, dateTime.LocalToday, request.SourceType, request.SourceId,
                request.SourceReference, lines, dateTime.UtcNow);
            await invoices.AddAsync(invoice, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return ToResult(invoice);
        }, cancellationToken);
    }

    /// <summary>
    /// Every part of the source's bill that holds no cash is cancelled. A part that was paid is kept and the refund team
    /// (Billing.Refund.Request) is told that a cancellation refund is needed, so the money is not left on the account.
    /// </summary>
    async Task<ChargeResult?> IChargeService.CancelAsync(string sourceType, Guid sourceId, string reason, CancellationToken cancellationToken)
    {
        var parts = await invoices.GetBySourceAsync(sourceType, sourceId, cancellationToken);
        if (parts.Count == 0)
        {
            return null;
        }

        var paid = new List<Invoice>();
        Invoice? last = null;
        foreach (var part in parts)
        {
            last = await LockedAsync(part.Id, (invoice, _) =>
            {
                if (invoice.Status == InvoiceStatus.Cancelled || invoice.CancellationRequested)
                {
                    return Task.CompletedTask;
                }

                if (invoice.CashHeldAmount <= 0)
                {
                    invoice.Cancel(reason, dateTime.UtcNow);
                }
                else
                {
                    paid.Add(invoice);
                }

                return Task.CompletedTask;
            }, cancellationToken);
        }

        foreach (var invoice in paid)
        {
            await notifications.NotifyPermissionAsync(BillingPermissions.RefundRequest, new NotificationMessage(NotificationTypes.RefundApproval,
                $"Refund needed · {invoice.InvoiceNumber}",
                $"{reason} — {invoice.CashHeldAmount:0.00} was paid on this invoice. Raise a cancellation refund to return it.",
                $"/billing/invoices/{invoice.Id}"), cancellationToken);
        }

        return ToResult(last!);
    }

    /// <summary>
    /// Charges go on the source's open running bill. Once that bill is finalized (or cancelled), a later charge opens a
    /// supplementary running bill (next part number) for the cashier to finalize and collect, so it is never lost. A charge
    /// key already on any part of the bill is skipped, whichever part it is on.
    /// </summary>
    async Task<BillStatus> IChargeService.AddToRunningBillAsync(RunningChargeRequest request, CancellationToken cancellationToken)
    {
        var charges = request.Lines.Select(l => new InvoiceLineValues(null, null, null, l.Description, ToCategory(l.Category), l.Quantity,
            l.UnitPrice, l.Key)).ToList();

        // Allocated outside the transaction only when a new bill is needed (a lost race leaves a gap, never a duplicate).
        var known = await invoices.GetBySourceAsync(request.SourceType, request.SourceId, cancellationToken);
        var number = NeedsNewBill(known, charges) ? await numberSequences.NextAsync(BillingNumberSequences.Invoice, cancellationToken) : null;

        Invoice? opened = null;
        var status = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await invoices.LockPatientAsync(request.PatientId, ct);
            var parts = await invoices.GetBySourceAsync(request.SourceType, request.SourceId, ct);
            var fresh = charges.Where(c => !parts.Any(p => p.Lines.Any(l => l.ChargeKey == c.ChargeKey))).ToList();

            Invoice bill;
            if (parts.FirstOrDefault(p => p.Status == InvoiceStatus.Draft) is { } open)
            {
                bill = await invoices.GetWithDetailsAsync(open.Id, ct) ?? throw new NotFoundException("Invoice", open.Id);
            }
            else if (parts.Count > 0 && fresh.Count == 0)
            {
                return ToBillStatus(parts);
            }
            else
            {
                var part = parts.Count == 0 ? 1 : parts.Max(p => p.SourcePart) + 1;
                bill = Invoice.CreateRunning(number ?? await numberSequences.NextAsync(BillingNumberSequences.Invoice, ct), request.PatientId,
                    dateTime.LocalToday, request.SourceType, request.SourceId, request.SourceReference, part);
                await invoices.AddAsync(bill, ct);
                opened = bill.IsSupplementary ? bill : null;
            }

            bill.AddCharges(fresh);
            await unitOfWork.SaveChangesAsync(ct);
            return ToBillStatus([.. parts.Where(p => p.Id != bill.Id), bill]);
        }, cancellationToken);

        if (opened is { } supplementary)
        {
            await notifications.NotifyPermissionAsync(BillingPermissions.PaymentCollect, new NotificationMessage(NotificationTypes.FinalBillReady,
                $"Supplementary bill · {supplementary.InvoiceNumber}",
                $"{request.SourceReference}: {supplementary.NetAmount:0.00} charged after the final bill. Finalize it and collect at the counter.",
                $"/billing/invoices/{supplementary.Id}"), cancellationToken);
        }

        return status;
    }

    /// <summary>Finalizes every part of the source's bill that is still a draft (no-op when none is).</summary>
    async Task<BillStatus> IChargeService.FinalizeRunningBillAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken)
    {
        var known = await invoices.GetBySourceAsync(sourceType, sourceId, cancellationToken);
        if (known.Count == 0)
        {
            throw new BusinessException(BillingErrorCodes.NoRunningBill, "There is no bill for this stay yet.");
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await invoices.LockPatientAsync(known[0].PatientId, ct);
            var parts = new List<Invoice>();
            foreach (var part in await invoices.GetBySourceAsync(sourceType, sourceId, ct))
            {
                if (part.Status != InvoiceStatus.Draft)
                {
                    parts.Add(part);
                    continue;
                }

                var bill = await invoices.GetWithDetailsAsync(part.Id, ct) ?? throw new NotFoundException("Invoice", part.Id);
                bill.Finalize(dateTime.UtcNow);
                parts.Add(bill);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return ToBillStatus(parts);
        }, cancellationToken);
    }

    async Task<BillStatus?> IChargeService.GetBillAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken) =>
        await invoices.GetBySourceAsync(sourceType, sourceId, cancellationToken) is { Count: > 0 } parts ? ToBillStatus(parts) : null;

    /// <summary>A new bill is needed when no part is open and a charge key is not on any part yet (or no bill exists).</summary>
    private static bool NeedsNewBill(IReadOnlyList<Invoice> parts, IReadOnlyList<InvoiceLineValues> charges) =>
        parts.All(p => p.Status != InvoiceStatus.Draft)
        && (parts.Count == 0 || charges.Any(c => !parts.Any(p => p.Lines.Any(l => l.ChargeKey == c.ChargeKey))));

    /// <summary>
    /// The source's bill as other modules see it: the latest part's identity and status, with amounts summed over the parts
    /// that are not cancelled, so a supplementary bill's due counts (e.g. in the IPD release check). While a part is still a
    /// draft, its "due" is everything charged so far (deposits sit in the advance until settlement).
    /// </summary>
    private static BillStatus ToBillStatus(IReadOnlyList<Invoice> parts)
    {
        var latest = parts[^1];
        var live = parts.Where(p => p.Status != InvoiceStatus.Cancelled).ToList();
        return new BillStatus(latest.Id, latest.InvoiceNumber, latest.Status.ToString(), live.Sum(p => p.Subtotal), live.Sum(p => p.NetAmount),
            live.Sum(p => p.PaidAmount), live.Sum(p => p.Status == InvoiceStatus.Finalized ? p.DueAmount : p.NetAmount - p.PaidAmount),
            live.Sum(p => p.Lines.Count));
    }

    private static ServiceCategory ToCategory(string category) =>
        Enum.TryParse<ServiceCategory>(category, out var parsed) ? parsed : ServiceCategory.Other;

    /// <summary>A running bill is finalized from the ward; a supplementary bill (charges after the final bill) is closed by the cashier.</summary>
    private static void EnsureNotRunningBill(Invoice invoice)
    {
        if (invoice.IsRunningBill && !invoice.IsSupplementary)
        {
            throw new BusinessException(BillingErrorCodes.RunningBill,
                "This bill is still collecting charges for an ongoing stay. It is finalized from the ward at discharge.");
        }
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
