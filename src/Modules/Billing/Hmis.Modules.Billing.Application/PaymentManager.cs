using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Billing.Domain.Entities;

namespace Hmis.Modules.Billing.Application;

/// <summary>
/// Payments, advance deposits, refunds, patient accounts, approvals and collection reports. Every money movement runs
/// under the patient's billing lock, so amounts due and advance balances cannot be spent twice.
/// </summary>
public sealed class PaymentManager(
    IPaymentRepository payments,
    IRefundRepository refunds,
    IInvoiceRepository invoices,
    IBillingUnitOfWork unitOfWork,
    BillingDtoFactory dtos,
    IPatientLookup patientLookup,
    INumberSequenceService numberSequences,
    IPermissionChecker permissions,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IPaymentManager
{
    // ---------- Payments ----------

    /// <summary>Idempotent per ClientRequestId: a retried submission returns the original receipt instead of charging twice.</summary>
    public async Task<ReceiptDto> CollectAsync(PaymentCreateDto request, CancellationToken cancellationToken)
    {
        if (await payments.GetByClientRequestAsync(request.ClientRequestId, cancellationToken) is { } done)
        {
            return await ReplayAsync(done, request, cancellationToken);
        }

        var patient = await patientLookup.GetAsync(request.PatientId, cancellationToken)
            ?? throw new BusinessException(BillingErrorCodes.PatientUnavailable, "Select a registered patient.", "patientId");
        if (request.InvoiceId is null && patient.Status != "Active")
        {
            throw new BusinessException(BillingErrorCodes.PatientUnavailable, "Advance deposits need an active patient record.", "patientId");
        }

        var number = await numberSequences.NextAsync(BillingNumberSequences.Receipt, cancellationToken);
        var (payment, replayed) = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await invoices.LockPatientAsync(request.PatientId, ct);
            if (await payments.GetByClientRequestAsync(request.ClientRequestId, ct) is { } raced)
            {
                return (raced, true);
            }

            var received = Payment.Receive(number, request.ClientRequestId, request.PatientId, request.InvoiceId, request.Amount,
                request.Method, request.Reference, request.Notes, dateTime.UtcNow);

            if (request.InvoiceId is { } invoiceId)
            {
                var invoice = await GetInvoiceForPatientAsync(invoiceId, request.PatientId, ct);
                if (request.Method == PaymentMethod.Advance)
                {
                    var available = await payments.GetAdvanceBalanceAsync(request.PatientId, ct);
                    if (request.Amount > available)
                    {
                        throw new BusinessException(BillingErrorCodes.AdvanceInsufficient,
                            $"The patient's available advance is {available:0.00}.", "amount");
                    }
                }

                invoice.RecordPayment(request.Amount, request.Method == PaymentMethod.Advance);
            }

            await payments.AddAsync(received, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return (received, false);
        }, cancellationToken);

        return replayed ? await ReplayAsync(payment, request, cancellationToken) : await ReceiptAsync(payment, cancellationToken);
    }

    public async Task<ReceiptDto> GetReceiptAsync(Guid paymentId, CancellationToken cancellationToken) =>
        await ReceiptAsync(await payments.GetByIdAsync(paymentId, cancellationToken) ?? throw new NotFoundException("Payment", paymentId),
            cancellationToken);

    // ---------- Refunds ----------

    public async Task<RefundDto> RequestRefundAsync(RefundCreateDto request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        if (await patientLookup.GetAsync(request.PatientId, cancellationToken) is null)
        {
            throw new BusinessException(BillingErrorCodes.PatientUnavailable, "Select a registered patient.", "patientId");
        }

        var canApprove = await HasPermissionAsync(BillingPermissions.RefundApprove, cancellationToken);
        var number = await numberSequences.NextAsync(BillingNumberSequences.Refund, cancellationToken);

        var refund = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await invoices.LockPatientAsync(request.PatientId, ct);
            decimal amount;
            if (request.Type == RefundType.InvoiceCancellation)
            {
                var invoice = await GetInvoiceForPatientAsync(request.InvoiceId!.Value, request.PatientId, ct);
                amount = invoice.RequestCancellationRefund();
            }
            else
            {
                amount = request.Amount ?? 0;
                var available = await payments.GetAdvanceBalanceAsync(request.PatientId, ct);
                if (amount > available)
                {
                    throw new BusinessException(BillingErrorCodes.RefundExceedsAdvance,
                        $"The patient's available advance is {available:0.00}.", "amount");
                }
            }

            var requested = Refund.Request(number, request.PatientId, request.InvoiceId, request.Type, amount, request.Reason);
            if (canApprove)
            {
                // The requester holds approval rights: their request is the approval (still paid out separately).
                requested.Approve(userId, null, dateTime.UtcNow);
            }

            await refunds.AddAsync(requested, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return requested;
        }, cancellationToken);

        return await dtos.RefundAsync(refund, cancellationToken);
    }

    public Task<RefundDto> ApproveRefundAsync(Guid id, DecisionDto request, CancellationToken cancellationToken) =>
        ChangeRefundAsync(id, (refund, userId, _) =>
        {
            refund.Approve(userId, request.Note, dateTime.UtcNow);
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<RefundDto> RejectRefundAsync(Guid id, DecisionDto request, CancellationToken cancellationToken) =>
        ChangeRefundAsync(id, async (refund, userId, ct) =>
        {
            refund.Reject(userId, request.Note, dateTime.UtcNow);
            if (refund.InvoiceId is { } invoiceId)
            {
                (await GetInvoiceForPatientAsync(invoiceId, refund.PatientId, ct)).ReleaseCancellationRequest();
            }
        }, cancellationToken);

    /// <summary>The cashier hands the money back. A cancellation refund also cancels its invoice.</summary>
    public Task<RefundDto> PayRefundAsync(Guid id, RefundPayDto request, CancellationToken cancellationToken) =>
        ChangeRefundAsync(id, async (refund, userId, ct) =>
        {
            refund.MarkPaid(userId, request.Method, request.Reference, dateTime.UtcNow);
            if (refund.InvoiceId is { } invoiceId)
            {
                (await GetInvoiceForPatientAsync(invoiceId, refund.PatientId, ct))
                    .CompleteCancellationRefund(refund.Amount, $"Cancelled with refund {refund.RefundNumber}: {refund.Reason}", dateTime.UtcNow);
            }
        }, cancellationToken);

    // ---------- Account & approvals ----------

    public async Task<PatientAccountDto> GetAccountAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var patient = await patientLookup.GetAsync(patientId, cancellationToken) ?? throw new NotFoundException("Patient", patientId);
        var invoiceList = await invoices.GetForPatientAsync(patientId, BillingPolicy.AccountListLimit, cancellationToken);
        var paymentList = await payments.GetForPatientAsync(patientId, BillingPolicy.AccountListLimit, cancellationToken);
        var refundList = await refunds.GetForPatientAsync(patientId, BillingPolicy.AccountListLimit, cancellationToken);

        return new PatientAccountDto(
            BillingDtoFactory.Brief(patient)!,
            await invoices.GetOutstandingAsync(patientId, cancellationToken),
            await payments.GetAdvanceBalanceAsync(patientId, cancellationToken),
            await dtos.SummariesAsync(invoiceList, cancellationToken),
            await dtos.PaymentsAsync(paymentList, cancellationToken),
            await dtos.RefundsAsync(refundList, cancellationToken));
    }

    public async Task<ApprovalsDto> GetApprovalsAsync(CancellationToken cancellationToken)
    {
        var withDiscounts = await invoices.GetWithPendingDiscountsAsync(cancellationToken);
        var pending = withDiscounts.SelectMany(i => i.Discounts.Where(d => d.Status == ApprovalStatus.Pending).Select(d => (Invoice: i, Discount: d)))
            .OrderBy(x => x.Discount.CreatedAt)
            .ToList();
        var names = await dtos.LoadAsync(withDiscounts.Select(i => i.PatientId), pending.Select(x => x.Discount.CreatedBy), [], cancellationToken);

        var discounts = pending.Select(x =>
        {
            var patient = names.Patient(x.Invoice.PatientId);
            return new PendingDiscountDto(
                x.Discount.Id, x.Invoice.Id, x.Invoice.InvoiceNumber, patient?.MRNumber, patient?.FullName, x.Invoice.Subtotal,
                x.Invoice.DiscountAmount, x.Discount.Type, x.Discount.Value, x.Discount.Amount,
                Math.Round(x.Invoice.DiscountPercentWith(x.Discount.Amount), 1), x.Discount.Reason, names.User(x.Discount.CreatedBy),
                x.Discount.CreatedAt);
        }).ToList();

        return new ApprovalsDto(
            discounts,
            await dtos.RefundsAsync(await refunds.GetByStatusAsync(RefundStatus.Requested, cancellationToken), cancellationToken),
            await dtos.RefundsAsync(await refunds.GetByStatusAsync(RefundStatus.Approved, cancellationToken), cancellationToken));
    }

    // ---------- Collection report ----------

    public async Task<CollectionReportDto> GetCollectionReportAsync(CollectionQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var allCashiers = await HasPermissionAsync(BillingPermissions.ReportsView, cancellationToken);
        var cashierId = allCashiers ? query.CashierId : userId;

        var fromUtc = dateTime.StartOfLocalDayUtc(query.From);
        var toUtc = dateTime.StartOfLocalDayUtc(query.To.AddDays(1));
        var received = await payments.GetReceivedBetweenAsync(fromUtc, toUtc, cashierId, cancellationToken);
        var paidOut = await refunds.GetPaidBetweenAsync(fromUtc, toUtc, cashierId, cancellationToken);

        // Advance applied to an invoice moves no money; it was counted when the deposit was received.
        var money = received.Where(p => p.Method != PaymentMethod.Advance).ToList();
        var names = await dtos.LoadAsync(
            money.Select(p => p.PatientId).Concat(paidOut.Select(r => r.PatientId)),
            money.Select(p => p.CreatedBy).Concat(paidOut.Select(r => r.PaidBy)).Append(cashierId),
            money.Select(p => p.InvoiceId).Concat(paidOut.Select(r => r.InvoiceId)),
            cancellationToken);

        var byMethod = money.Select(p => (p.Method, In: p.Amount, Out: 0m))
            .Concat(paidOut.Select(r => (Method: r.Method!.Value, In: 0m, Out: r.Amount)))
            .GroupBy(x => x.Method)
            .OrderBy(g => g.Key)
            .Select(g => new CollectionMethodTotalDto(g.Key, g.Sum(x => x.In), g.Sum(x => x.Out), g.Sum(x => x.In - x.Out)))
            .ToList();

        var byCashier = money.Select(p => (User: p.CreatedBy, In: p.Amount, Out: 0m))
            .Concat(paidOut.Select(r => (User: r.PaidBy, In: 0m, Out: r.Amount)))
            .GroupBy(x => x.User)
            .Select(g => new CollectionCashierTotalDto(g.Key, names.User(g.Key), g.Sum(x => x.In), g.Sum(x => x.Out), g.Sum(x => x.In - x.Out)))
            .OrderBy(c => c.Name)
            .ToList();

        var transactions = money.Select(p => new CollectionTransactionDto(
                p.IsAdvanceDeposit ? "Deposit" : "Payment", p.Id, p.ReceiptNumber, p.ReceivedAt, names.Patient(p.PatientId)?.MRNumber,
                names.Patient(p.PatientId)?.FullName, names.Invoice(p.InvoiceId), p.Method, p.Reference, p.Amount, names.User(p.CreatedBy)))
            .Concat(paidOut.Select(r => new CollectionTransactionDto(
                "Refund", r.Id, r.RefundNumber, r.PaidAt!.Value, names.Patient(r.PatientId)?.MRNumber, names.Patient(r.PatientId)?.FullName,
                names.Invoice(r.InvoiceId), r.Method!.Value, r.Reference, -r.Amount, names.User(r.PaidBy))))
            .OrderBy(t => t.At)
            .ToList();

        var totalIn = money.Sum(p => p.Amount);
        var totalOut = paidOut.Sum(r => r.Amount);
        return new CollectionReportDto(
            query.From, query.To, cashierId, names.User(cashierId), allCashiers, totalIn, totalOut, totalIn - totalOut,
            received.Where(p => p.Method == PaymentMethod.Advance).Sum(p => p.Amount), byMethod, byCashier, transactions);
    }

    // ---------- helpers ----------

    private async Task<ReceiptDto> ReplayAsync(Payment done, PaymentCreateDto request, CancellationToken cancellationToken)
    {
        if (done.PatientId != request.PatientId || done.InvoiceId != request.InvoiceId || done.Amount != request.Amount || done.Method != request.Method)
        {
            throw new ConflictException("This payment request was already used for a different payment. Reopen the payment form.",
                BillingErrorCodes.DuplicateRequest);
        }

        return await ReceiptAsync(done, cancellationToken);
    }

    private async Task<ReceiptDto> ReceiptAsync(Payment payment, CancellationToken cancellationToken)
    {
        var invoice = payment.InvoiceId is { } invoiceId ? await invoices.GetWithDetailsAsync(invoiceId, cancellationToken) : null;
        var names = await dtos.LoadAsync([payment.PatientId], [payment.CreatedBy], [payment.InvoiceId], cancellationToken);
        return new ReceiptDto(
            BillingDtoFactory.Payment(payment, names),
            BillingDtoFactory.Brief(names.Patient(payment.PatientId)),
            invoice is null ? null : BillingDtoFactory.Summary(invoice, names),
            invoice is null ? [] : BillingDtoFactory.Lines(invoice),
            await payments.GetAdvanceBalanceAsync(payment.PatientId, cancellationToken));
    }

    /// <summary>Loads the refund under its patient's billing lock, applies the change and saves, in one transaction.</summary>
    private async Task<RefundDto> ChangeRefundAsync(Guid id, Func<Refund, Guid, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var patientId = await refunds.GetPatientIdAsync(id, cancellationToken) ?? throw new NotFoundException("Refund", id);
        var refund = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await invoices.LockPatientAsync(patientId, ct);
            var current = await refunds.GetByIdAsync(id, ct) ?? throw new NotFoundException("Refund", id);
            await change(current, userId, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return current;
        }, cancellationToken);

        return await dtos.RefundAsync(refund, cancellationToken);
    }

    private async Task<Invoice> GetInvoiceForPatientAsync(Guid invoiceId, Guid patientId, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetWithDetailsAsync(invoiceId, cancellationToken) ?? throw new NotFoundException("Invoice", invoiceId);
        return invoice.PatientId == patientId
            ? invoice
            : throw new BusinessException(BillingErrorCodes.InvoicePatientMismatch, "This invoice belongs to another patient.", "invoiceId");
    }

    private Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken) =>
        permissions.HasPermissionAsync(currentUser.UserId ?? throw new UnauthorizedException(), permission, cancellationToken);
}
