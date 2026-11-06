using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Billing.Domain.Entities;

namespace Hmis.Modules.Billing.Application;

/// <summary>Builds billing DTOs, resolving patient names, user names and invoice numbers in a few batched lookups.</summary>
public sealed class BillingDtoFactory(
    IPatientLookup patientLookup,
    IUserEmployeeLookup userLookup,
    IInvoiceRepository invoices,
    IPaymentRepository payments,
    IRefundRepository refunds)
{
    public sealed record Names(
        IReadOnlyDictionary<Guid, PatientSummary> Patients,
        IReadOnlyDictionary<Guid, string> Users,
        IReadOnlyDictionary<Guid, string> InvoiceNumbers)
    {
        public string? User(Guid? id) => id is { } value ? Users.GetValueOrDefault(value) : null;

        public string? Invoice(Guid? id) => id is { } value ? InvoiceNumbers.GetValueOrDefault(value) : null;

        public PatientSummary? Patient(Guid id) => Patients.GetValueOrDefault(id);
    }

    public async Task<Names> LoadAsync(IEnumerable<Guid> patientIds, IEnumerable<Guid?> userIds, IEnumerable<Guid?> invoiceIds,
        CancellationToken cancellationToken)
    {
        var patientList = patientIds.Distinct().ToList();
        var userList = userIds.OfType<Guid>().Distinct().ToList();
        var invoiceList = invoiceIds.OfType<Guid>().Distinct().ToList();

        var patients = patientList.Count == 0 ? [] : await patientLookup.GetManyAsync(patientList, cancellationToken);
        var users = userList.Count == 0 ? new Dictionary<Guid, string>() : await userLookup.GetFullNamesAsync(userList, cancellationToken);
        var numbers = invoiceList.Count == 0 ? [] : await invoices.GetManyAsync(invoiceList, cancellationToken);
        return new Names(patients.ToDictionary(p => p.Id), users, numbers.ToDictionary(i => i.Id, i => i.InvoiceNumber));
    }

    public async Task<InvoiceDto> InvoiceAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var invoicePayments = await payments.GetForInvoiceAsync(invoice.Id, cancellationToken);
        var invoiceRefunds = await refunds.GetForInvoiceAsync(invoice.Id, cancellationToken);
        var advance = await payments.GetAdvanceBalanceAsync(invoice.PatientId, cancellationToken);
        var names = await LoadAsync(
            [invoice.PatientId],
            [invoice.CreatedBy, .. invoice.Discounts.SelectMany(d => new[] { d.CreatedBy, d.DecidedBy }),
                .. invoicePayments.Select(p => p.CreatedBy), .. invoiceRefunds.SelectMany(r => new[] { r.CreatedBy, r.DecidedBy, r.PaidBy })],
            [invoice.Id],
            cancellationToken);

        return new InvoiceDto(
            invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate, Brief(names.Patient(invoice.PatientId)), invoice.Status,
            invoice.PaymentStatus, invoice.SourceType, invoice.SourceReference, invoice.Notes, invoice.Subtotal, invoice.DiscountAmount,
            invoice.NetAmount, invoice.PaidAmount, invoice.AdvanceAppliedAmount, invoice.RefundedAmount, invoice.DueAmount,
            invoice.CancellationRequested, invoice.IsRunningBill, invoice.IsSupplementary, invoice.FinalizedAt, invoice.CancelledAt,
            invoice.CancellationReason, invoice.CreatedAt,
            names.User(invoice.CreatedBy),
            Lines(invoice),
            invoice.Discounts.OrderBy(d => d.CreatedAt).Select(d => new DiscountDto(
                d.Id, d.Type, d.Value, d.Amount, d.Reason, d.Status, names.User(d.CreatedBy), d.CreatedAt, names.User(d.DecidedBy),
                d.DecidedAt, d.DecisionNote)).ToList(),
            invoicePayments.Select(p => Payment(p, names)).ToList(),
            invoiceRefunds.Select(r => Refund(r, names)).ToList(),
            advance,
            invoice.RowVersion);
    }

    public async Task<IReadOnlyList<InvoiceSummaryDto>> SummariesAsync(IReadOnlyList<Invoice> list, CancellationToken cancellationToken)
    {
        var names = await LoadAsync(list.Select(i => i.PatientId), [], [], cancellationToken);
        return list.Select(i => Summary(i, names)).ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> PaymentsAsync(IReadOnlyList<Payment> list, CancellationToken cancellationToken)
    {
        var names = await LoadAsync([], list.Select(p => p.CreatedBy), list.Select(p => p.InvoiceId), cancellationToken);
        return list.Select(p => Payment(p, names)).ToList();
    }

    public async Task<IReadOnlyList<RefundDto>> RefundsAsync(IReadOnlyList<Refund> list, CancellationToken cancellationToken)
    {
        var names = await LoadAsync(
            list.Select(r => r.PatientId),
            list.SelectMany(r => new[] { r.CreatedBy, r.DecidedBy, r.PaidBy }),
            list.Select(r => r.InvoiceId),
            cancellationToken);
        return list.Select(r => Refund(r, names)).ToList();
    }

    public async Task<RefundDto> RefundAsync(Refund refund, CancellationToken cancellationToken) =>
        (await RefundsAsync([refund], cancellationToken))[0];

    public static InvoiceSummaryDto Summary(Invoice i, Names names)
    {
        var patient = names.Patient(i.PatientId);
        return new InvoiceSummaryDto(
            i.Id, i.InvoiceNumber, i.InvoiceDate, i.PatientId, patient?.MRNumber, patient?.FullName, i.Status, i.PaymentStatus, i.SourceType,
            i.SourceReference, i.Subtotal, i.DiscountAmount, i.NetAmount, i.PaidAmount, i.DueAmount, i.CancellationRequested, i.IsSupplementary);
    }

    public static IReadOnlyList<InvoiceLineDto> Lines(Invoice invoice) =>
        invoice.Lines.OrderBy(l => l.SortOrder)
            .Select(l => new InvoiceLineDto(l.Id, l.ServiceId, l.ServiceCode, l.Description, l.Category, l.Quantity, l.UnitPrice, l.LineTotal,
                l.ChargeKey is not null))
            .ToList();

    public static PaymentDto Payment(Payment p, Names names) =>
        new(p.Id, p.ReceiptNumber, p.PatientId, p.InvoiceId, names.Invoice(p.InvoiceId), p.Amount, p.Method, p.Reference, p.Notes,
            p.ReceivedAt, names.User(p.CreatedBy));

    public static RefundDto Refund(Refund r, Names names)
    {
        var patient = names.Patient(r.PatientId);
        return new RefundDto(
            r.Id, r.RefundNumber, r.PatientId, patient?.MRNumber, patient?.FullName, r.InvoiceId, names.Invoice(r.InvoiceId), r.Type, r.Amount,
            r.Reason, r.Status, names.User(r.CreatedBy), r.CreatedAt, names.User(r.DecidedBy), r.DecidedAt, r.DecisionNote,
            names.User(r.PaidBy), r.PaidAt, r.Method, r.Reference);
    }

    public static PatientBriefDto? Brief(PatientSummary? p) =>
        p is null ? null : new PatientBriefDto(p.Id, p.MRNumber, p.FullName, p.Gender, p.DateOfBirth, p.MobileNumber, p.Status);
}
