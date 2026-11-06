using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Billing.Domain.Entities;

public enum InvoiceStatus
{
    Draft,
    Finalized,
    Cancelled
}

/// <summary>Derived from the status and amounts; not stored.</summary>
public enum PaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,

    /// <summary>Cancelled with no cash returned (nothing was paid, or only advance that went back to the patient's advance).</summary>
    Cancelled,

    /// <summary>Cancelled after the cash received was refunded.</summary>
    Refunded
}

public enum DiscountType
{
    Amount,
    Percent
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>Values for one invoice line; Id is null for a new line. ChargeKey identifies a line posted by another module.</summary>
public sealed record InvoiceLineValues(
    Guid? Id,
    Guid? ServiceId,
    string? ServiceCode,
    string Description,
    ServiceCategory Category,
    int Quantity,
    decimal UnitPrice,
    string? ChargeKey = null);

/// <summary>
/// A patient invoice. Lines are editable only while Draft. A finalized invoice is never changed or deleted (BRD Module 17
/// financial rules 4, 5 and 8): discounts are separate approved entries, payments and refunds are separate transactions,
/// and cancellation is a status change with a reason.
/// </summary>
[Audited]
public sealed class Invoice : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private readonly List<InvoiceLine> _lines = [];
    private readonly List<InvoiceDiscount> _discounts = [];

    private Invoice() { }

    public string InvoiceNumber { get; private set; } = string.Empty;
    public Guid PatientId { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>Set for invoices posted by another module (e.g. the OPD consultation fee); null for manual invoices.</summary>
    public string? SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public string? SourceReference { get; private set; }

    /// <summary>
    /// Part number of the source's bill: 1 for the bill itself, 2+ for a supplementary bill opened for charges that
    /// arrived after the earlier part was finalized. Unique per source together with <see cref="SourceId"/>.
    /// </summary>
    public int SourcePart { get; private set; } = 1;

    public string? Notes { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal NetAmount { get; private set; }

    /// <summary>Everything received against the invoice, including advance applied.</summary>
    public decimal PaidAmount { get; private set; }

    /// <summary>The part of <see cref="PaidAmount"/> taken from the patient's advance (released if the invoice is cancelled).</summary>
    public decimal AdvanceAppliedAmount { get; private set; }

    /// <summary>Cash returned through a cancellation refund.</summary>
    public decimal RefundedAmount { get; private set; }

    public DateTime? FinalizedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>A cancellation refund is waiting for approval or payout; payments and discounts are blocked meanwhile.</summary>
    public bool CancellationRequested { get; private set; }

    /// <summary>Incremented on every change so the RowVersion check also covers line-only edits.</summary>
    public int Revision { get; private set; }

    public uint RowVersion { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;
    public IReadOnlyList<InvoiceDiscount> Discounts => _discounts;

    public decimal DueAmount => Status == InvoiceStatus.Finalized ? NetAmount - PaidAmount : 0;

    /// <summary>Money actually held for this invoice (excluding advance applied and refunds already returned).</summary>
    public decimal CashHeldAmount => PaidAmount - AdvanceAppliedAmount - RefundedAmount;

    /// <summary>A draft posted to by another module (e.g. an IPD stay); it is finalized by that module, not by the cashier.</summary>
    public bool IsRunningBill => SourceType is not null && Status == InvoiceStatus.Draft;

    /// <summary>Charges that arrived after the source's bill was finalized; the cashier finalizes and collects it.</summary>
    public bool IsSupplementary => SourceType is not null && SourcePart > 1;

    /// <summary>A finalized invoice with nothing left to pay (including a zero-amount bill) is settled.</summary>
    public PaymentStatus PaymentStatus => Status switch
    {
        InvoiceStatus.Cancelled => RefundedAmount > 0 ? PaymentStatus.Refunded : PaymentStatus.Cancelled,
        InvoiceStatus.Finalized when PaidAmount >= NetAmount => PaymentStatus.Paid,
        _ => PaidAmount <= 0 ? PaymentStatus.Unpaid : PaymentStatus.PartiallyPaid
    };

    public static Invoice CreateDraft(string invoiceNumber, Guid patientId, DateOnly invoiceDate, string? notes) =>
        new()
        {
            InvoiceNumber = invoiceNumber,
            PatientId = patientId,
            InvoiceDate = invoiceDate,
            Notes = Clean(notes)
        };

    /// <summary>An invoice posted by another module: created finalized, because the charge is already decided.</summary>
    public static Invoice CreateFromCharge(string invoiceNumber, Guid patientId, DateOnly invoiceDate, string sourceType, Guid sourceId,
        string sourceReference, IReadOnlyList<InvoiceLineValues> lines, DateTime utcNow)
    {
        var invoice = CreateDraft(invoiceNumber, patientId, invoiceDate, null);
        invoice.SourceType = sourceType;
        invoice.SourceId = sourceId;
        invoice.SourceReference = sourceReference;
        invoice.SetLines(lines);
        invoice.Finalize(utcNow);
        return invoice;
    }

    /// <summary>
    /// A draft that another module adds charges to until it is finalized (e.g. an IPD stay). A <paramref name="sourcePart"/>
    /// above 1 opens a supplementary bill for charges that arrive after the earlier part was finalized.
    /// </summary>
    public static Invoice CreateRunning(string invoiceNumber, Guid patientId, DateOnly invoiceDate, string sourceType, Guid sourceId,
        string sourceReference, int sourcePart = 1)
    {
        if (sourcePart < 1)
        {
            throw new DomainException("INVOICE_PART_INVALID", "A bill part number starts at 1.");
        }

        var invoice = CreateDraft(invoiceNumber, patientId, invoiceDate, null);
        invoice.SourceType = sourceType;
        invoice.SourceId = sourceId;
        invoice.SourceReference = sourceReference;
        invoice.SourcePart = sourcePart;
        return invoice;
    }

    /// <summary>Appends posted charges whose key is not on the bill yet; returns how many were added.</summary>
    public int AddCharges(IReadOnlyList<InvoiceLineValues> charges)
    {
        EnsureDraft();
        var existing = _lines.Where(l => l.ChargeKey is not null).Select(l => l.ChargeKey!).ToHashSet();
        var added = 0;
        foreach (var charge in charges.Where(c => c.ChargeKey is not null && !existing.Contains(c.ChargeKey)))
        {
            _lines.Add(InvoiceLine.Create(Id, charge, _lines.Count == 0 ? 0 : _lines.Max(l => l.SortOrder) + 1));
            existing.Add(charge.ChargeKey!);
            added++;
        }

        if (added > 0)
        {
            Subtotal = _lines.Sum(l => l.LineTotal);
            NetAmount = Subtotal - DiscountAmount;
            Revision++;
        }

        return added;
    }

    public void UpdateNotes(string? notes)
    {
        EnsureDraft();
        Notes = Clean(notes);
        Revision++;
    }

    /// <summary>Updates lines with a known id, adds new ones and removes the rest, keeping the submitted order.</summary>
    public void SetLines(IReadOnlyList<InvoiceLineValues> values)
    {
        EnsureDraft();
        var keep = values.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        if (keep.Except(_lines.Select(l => l.Id)).Any())
        {
            throw new DomainException("INVOICE_LINE_UNKNOWN", "One of the lines no longer exists. Reload the invoice and try again.");
        }

        _lines.RemoveAll(l => !keep.Contains(l.Id));
        for (var order = 0; order < values.Count; order++)
        {
            var v = values[order];
            if (v.Id is { } id)
            {
                _lines.First(l => l.Id == id).Update(v, order);
            }
            else
            {
                _lines.Add(InvoiceLine.Create(Id, v, order));
            }
        }

        Subtotal = _lines.Sum(l => l.LineTotal);
        NetAmount = Subtotal - DiscountAmount;
        Revision++;
    }

    /// <summary>A bill with lines can be finalized even at a zero total (nothing to pay); an empty one cannot.</summary>
    public void Finalize(DateTime utcNow)
    {
        EnsureDraft();
        if (_lines.Count == 0)
        {
            throw new DomainException("INVOICE_EMPTY", "Add at least one line before finalizing.");
        }

        Status = InvoiceStatus.Finalized;
        FinalizedAt = utcNow;
        Revision++;
    }

    /// <summary>
    /// Records a discount request. When <paramref name="approvedBy"/> is set (the requester may approve it themselves)
    /// the discount applies at once; otherwise it waits for approval.
    /// </summary>
    public InvoiceDiscount RequestDiscount(DiscountType type, decimal value, string reason, Guid? approvedBy, DateTime utcNow)
    {
        EnsurePayable();
        if (_discounts.Any(d => d.Status == ApprovalStatus.Pending))
        {
            throw new DomainException("DISCOUNT_PENDING", "A discount on this invoice is already waiting for approval.");
        }

        var amount = CalculateDiscount(type, value);
        EnsureDiscountFits(amount);
        var discount = InvoiceDiscount.Create(Id, type, value, amount, reason);
        _discounts.Add(discount);
        if (approvedBy is { } approver)
        {
            ApplyDiscount(discount, approver, null, utcNow);
        }

        Revision++;
        return discount;
    }

    /// <summary>Discount percentage of the subtotal after adding <paramref name="amount"/> to discounts already given.</summary>
    public decimal DiscountPercentWith(decimal amount) => Subtotal <= 0 ? 0 : (DiscountAmount + amount) * 100 / Subtotal;

    public decimal CalculateDiscount(DiscountType type, decimal value)
    {
        var amount = type == DiscountType.Percent ? Money.Round(Subtotal * value / 100) : Money.Round(value);
        if (amount <= 0 || (type == DiscountType.Percent && value > 100))
        {
            throw new DomainException("DISCOUNT_INVALID", "Enter a discount greater than zero (at most 100%).");
        }

        return amount;
    }

    public void ApproveDiscount(Guid discountId, Guid approverId, string? note, DateTime utcNow)
    {
        var discount = GetPendingDiscount(discountId);
        EnsurePayable();
        EnsureDiscountFits(discount.Amount);
        ApplyDiscount(discount, approverId, note, utcNow);
        Revision++;
    }

    public void RejectDiscount(Guid discountId, Guid approverId, string? note, DateTime utcNow)
    {
        GetPendingDiscount(discountId).Decide(ApprovalStatus.Rejected, approverId, note, utcNow);
        Revision++;
    }

    /// <summary>Money received against this invoice; never more than the amount due.</summary>
    public void RecordPayment(decimal amount, bool fromAdvance)
    {
        EnsurePayable();
        if (amount > DueAmount)
        {
            throw new DomainException("PAYMENT_EXCEEDS_DUE", $"The amount due is {DueAmount:0.00}; a payment cannot be more than that.");
        }

        PaidAmount += amount;
        if (fromAdvance)
        {
            AdvanceAppliedAmount += amount;
        }

        Revision++;
    }

    /// <summary>
    /// Starts "cancel and refund": the cash held is returned through an approved refund, after which the invoice is
    /// cancelled. Returns the amount to refund.
    /// </summary>
    public decimal RequestCancellationRefund()
    {
        EnsurePayable();
        if (CashHeldAmount <= 0)
        {
            throw new DomainException("INVOICE_NOTHING_TO_REFUND", "No cash was received for this invoice. Cancel it directly instead.");
        }

        CancellationRequested = true;
        Revision++;
        return CashHeldAmount;
    }

    public void ReleaseCancellationRequest()
    {
        CancellationRequested = false;
        Revision++;
    }

    /// <summary>The cancellation refund was paid out: record it and cancel the invoice.</summary>
    public void CompleteCancellationRefund(decimal refundedAmount, string reason, DateTime utcNow)
    {
        RefundedAmount += refundedAmount;
        CancellationRequested = false;
        Cancel(reason, utcNow);
    }

    /// <summary>
    /// Cancels the invoice. A finalized invoice can only be cancelled while no cash is held for it (advance applied is
    /// released back to the patient's advance); otherwise use a cancellation refund.
    /// </summary>
    public void Cancel(string reason, DateTime utcNow)
    {
        if (Status == InvoiceStatus.Cancelled)
        {
            throw new DomainException("INVOICE_CANCELLED", "This invoice is already cancelled.");
        }

        if (CancellationRequested)
        {
            throw new DomainException("INVOICE_REFUND_PENDING", "A cancellation refund for this invoice is in progress.");
        }

        if (CashHeldAmount > 0)
        {
            throw new DomainException("INVOICE_HAS_PAYMENTS", "Money was received for this invoice. Request a cancellation refund instead.");
        }

        foreach (var pending in _discounts.Where(d => d.Status == ApprovalStatus.Pending))
        {
            pending.Decide(ApprovalStatus.Rejected, null, "Invoice cancelled.", utcNow);
        }

        Status = InvoiceStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = reason.Trim();
        Revision++;
    }

    private void ApplyDiscount(InvoiceDiscount discount, Guid approverId, string? note, DateTime utcNow)
    {
        discount.Decide(ApprovalStatus.Approved, approverId, note, utcNow);
        DiscountAmount += discount.Amount;
        NetAmount = Subtotal - DiscountAmount;
    }

    private void EnsureDiscountFits(decimal amount)
    {
        if (amount > DueAmount)
        {
            throw new DomainException("DISCOUNT_EXCEEDS_DUE", $"The discount cannot be more than the amount due ({DueAmount:0.00}).");
        }
    }

    private InvoiceDiscount GetPendingDiscount(Guid discountId)
    {
        var discount = _discounts.FirstOrDefault(d => d.Id == discountId)
            ?? throw new DomainException("DISCOUNT_NOT_FOUND", "The discount was not found on this invoice.");
        return discount.Status == ApprovalStatus.Pending
            ? discount
            : throw new DomainException("DISCOUNT_DECIDED", "This discount has already been decided.");
    }

    private void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new DomainException("INVOICE_FINALIZED", "Only draft invoices can be changed.");
        }
    }

    private void EnsurePayable()
    {
        switch (Status)
        {
            case InvoiceStatus.Draft:
                throw new DomainException("INVOICE_NOT_FINALIZED", "Finalize the invoice first.");
            case InvoiceStatus.Cancelled:
                throw new DomainException("INVOICE_CANCELLED", "This invoice is cancelled.");
        }

        if (CancellationRequested)
        {
            throw new DomainException("INVOICE_REFUND_PENDING", "A cancellation refund for this invoice is in progress.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[Audited]
public sealed class InvoiceLine : AuditableEntity
{
    private InvoiceLine() { }

    public Guid InvoiceId { get; private set; }
    public int SortOrder { get; private set; }
    public Guid? ServiceId { get; private set; }
    public string? ServiceCode { get; private set; }

    /// <summary>Set for charges posted by another module; such lines are never edited by the cashier.</summary>
    public string? ChargeKey { get; private set; }

    public string Description { get; private set; } = string.Empty;
    public ServiceCategory Category { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    internal static InvoiceLine Create(Guid invoiceId, InvoiceLineValues values, int sortOrder)
    {
        var line = new InvoiceLine { InvoiceId = invoiceId };
        line.Update(values, sortOrder);
        return line;
    }

    internal void Update(InvoiceLineValues v, int sortOrder)
    {
        if (v.Quantity < 1 || v.UnitPrice < 0)
        {
            throw new DomainException("INVOICE_LINE_INVALID", $"Check the quantity and price of {v.Description.Trim()}.");
        }

        SortOrder = sortOrder;
        ChargeKey ??= v.ChargeKey;
        ServiceId = v.ServiceId;
        ServiceCode = v.ServiceCode;
        Description = v.Description.Trim();
        Category = v.Category;
        Quantity = v.Quantity;
        UnitPrice = Money.Round(v.UnitPrice);
        LineTotal = UnitPrice * Quantity;
    }
}

/// <summary>A discount request on a finalized invoice; only approved entries reduce the amount due.</summary>
[Audited]
public sealed class InvoiceDiscount : AuditableEntity
{
    private InvoiceDiscount() { }

    public Guid InvoiceId { get; private set; }
    public DiscountType Type { get; private set; }

    /// <summary>The amount or percentage entered.</summary>
    public decimal Value { get; private set; }

    /// <summary>The discount in money, fixed when requested.</summary>
    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = string.Empty;
    public ApprovalStatus Status { get; private set; } = ApprovalStatus.Pending;
    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    internal static InvoiceDiscount Create(Guid invoiceId, DiscountType type, decimal value, decimal amount, string reason) =>
        new() { InvoiceId = invoiceId, Type = type, Value = value, Amount = amount, Reason = reason.Trim() };

    internal void Decide(ApprovalStatus status, Guid? decidedBy, string? note, DateTime utcNow)
    {
        Status = status;
        DecidedBy = decidedBy;
        DecidedAt = utcNow;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
