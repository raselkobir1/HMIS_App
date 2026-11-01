using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.Modules.Billing.Domain.Entities;

namespace Hmis.Modules.Billing.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class BillingApplicationMarker;

public static class BillingPermissions
{
    public const string Module = "Billing";

    public const string View = "Billing.View";
    public const string ServicesManage = "Billing.Services.Manage";
    public const string InvoiceManage = "Billing.Invoice.Manage";
    public const string InvoiceCancel = "Billing.Invoice.Cancel";
    public const string PaymentCollect = "Billing.Payment.Collect";
    public const string DiscountApply = "Billing.Discount.Apply";
    public const string DiscountApprove = "Billing.Discount.Approve";
    public const string RefundRequest = "Billing.Refund.Request";
    public const string RefundApprove = "Billing.Refund.Approve";
    public const string ReportsView = "Billing.Reports.View";
    public const string SettingsManage = "Billing.Settings.Manage";
}

public sealed class BillingPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(BillingPermissions.View, BillingPermissions.Module, "View invoices, payments, refunds and patient accounts",
            [SystemRoles.Accountant, SystemRoles.HospitalAdministrator]),
        new(BillingPermissions.ServicesManage, BillingPermissions.Module, "Manage the service price list", [SystemRoles.HospitalAdministrator]),
        new(BillingPermissions.InvoiceManage, BillingPermissions.Module, "Create, edit, finalize and discard draft invoices", [SystemRoles.Accountant]),
        new(BillingPermissions.InvoiceCancel, BillingPermissions.Module, "Cancel finalized invoices", [SystemRoles.HospitalAdministrator]),
        new(BillingPermissions.PaymentCollect, BillingPermissions.Module, "Receive payments and advance deposits, apply advance",
            [SystemRoles.Accountant]),
        new(BillingPermissions.DiscountApply, BillingPermissions.Module,
            "Give discounts within the approval limit set in Billing settings (larger ones need approval)", [SystemRoles.Accountant]),
        new(BillingPermissions.DiscountApprove, BillingPermissions.Module, "Approve or reject discounts above the limit",
            [SystemRoles.HospitalAdministrator]),
        new(BillingPermissions.RefundRequest, BillingPermissions.Module, "Request refunds and pay out approved refunds", [SystemRoles.Accountant]),
        new(BillingPermissions.SettingsManage, BillingPermissions.Module, "Set the discount approval limit and the payment methods in use",
            [SystemRoles.HospitalAdministrator]),
        new(BillingPermissions.RefundApprove, BillingPermissions.Module, "Approve or reject refunds", [SystemRoles.HospitalAdministrator]),
        new(BillingPermissions.ReportsView, BillingPermissions.Module, "View collection reports for all cashiers",
            [SystemRoles.Accountant, SystemRoles.HospitalAdministrator, SystemRoles.HospitalOwner])
    ];
}

public static class BillingPolicy
{
    /// <summary>Total discount on an invoice above this share of the subtotal needs Billing.Discount.Approve (Q-BILL-4).</summary>
    public const decimal DiscountApprovalPercent = 10m;

    /// <summary>Longest period one collection report covers.</summary>
    public const int MaxReportDays = 31;

    /// <summary>Rows shown per list on the patient account page.</summary>
    public const int AccountListLimit = 100;
}

public static class BillingErrorCodes
{
    public const string PatientUnavailable = "BILLING_PATIENT_UNAVAILABLE";
    public const string ServiceUnavailable = "BILLING_SERVICE_UNAVAILABLE";
    public const string ServiceCodeExists = "SERVICE_CODE_EXISTS";
    public const string MethodDisabled = "PAYMENT_METHOD_DISABLED";
    public const string InvoicePatientMismatch = "INVOICE_PATIENT_MISMATCH";
    public const string AdvanceInsufficient = "PAYMENT_ADVANCE_INSUFFICIENT";
    public const string DuplicateRequest = "PAYMENT_DUPLICATE_REQUEST";
    public const string RefundExceedsAdvance = "REFUND_EXCEEDS_ADVANCE";
    public const string LineLocked = "INVOICE_LINE_LOCKED";
    public const string RunningBill = "INVOICE_RUNNING_BILL";
    public const string NoRunningBill = "INVOICE_NO_RUNNING_BILL";
}

/// <summary>Receipt and refund numbers (INVOICE is declared by Administration). Formats are editable in Settings.</summary>
public sealed class BillingNumberSequences : INumberSequenceDefinitionProvider
{
    public const string Invoice = "INVOICE";
    public const string Receipt = "RECEIPT";
    public const string Refund = "REFUND";

    public IEnumerable<NumberSequenceDefinition> GetSequences() =>
    [
        new(Receipt, "Money receipt number", "RCP", "TwoDigit", 6, "Yearly"),
        new(Refund, "Refund number", "RFD", "TwoDigit", 6, "Yearly")
    ];
}

// ---------- Service price list ----------

public sealed record BillableServiceDto(
    Guid Id, string Code, string Name, ServiceCategory Category, decimal Price, bool IsPriceEditable, bool IsActive, uint RowVersion);

public sealed record ServiceCreateDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ServiceCategory Category { get; init; }
    public decimal Price { get; init; }
    public bool IsPriceEditable { get; init; }
}

public sealed record ServiceUpdateDto
{
    public string Name { get; init; } = string.Empty;
    public ServiceCategory Category { get; init; }
    public decimal Price { get; init; }
    public bool IsPriceEditable { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record ServiceStatusDto
{
    public bool IsActive { get; init; }
}

public sealed class ServiceQuery : PaginationRequest
{
    public ServiceCategory? Category { get; set; }
    public bool? IsActive { get; set; }
}

// ---------- Invoices ----------

public sealed record PatientBriefDto(Guid Id, string MRNumber, string FullName, string Gender, DateOnly? DateOfBirth, string MobileNumber, string Status);

/// <param name="IsPosted">Posted by another module (e.g. daily bed rent); never edited by the cashier.</param>
public sealed record InvoiceLineDto(
    Guid Id, Guid? ServiceId, string? ServiceCode, string Description, ServiceCategory Category, int Quantity, decimal UnitPrice, decimal LineTotal,
    bool IsPosted);

/// <summary>A draft line: an active service from the price list. UnitPrice is used only for price-editable services.</summary>
public sealed record InvoiceLineInputDto
{
    public Guid? Id { get; init; }
    public Guid ServiceId { get; init; }
    public int Quantity { get; init; } = 1;
    public decimal? UnitPrice { get; init; }
}

public sealed record InvoiceCreateDto
{
    public Guid PatientId { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<InvoiceLineInputDto> Lines { get; init; } = [];
}

public sealed record InvoiceUpdateDto
{
    public string? Notes { get; init; }
    public IReadOnlyList<InvoiceLineInputDto> Lines { get; init; } = [];
    public uint RowVersion { get; init; }
}

public sealed record RowVersionDto
{
    public uint RowVersion { get; init; }
}

public sealed record InvoiceCancelDto
{
    public string Reason { get; init; } = string.Empty;
}

public sealed record DiscountRequestDto
{
    public DiscountType Type { get; init; }
    public decimal Value { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record DecisionDto
{
    public string? Note { get; init; }
}

public sealed record DiscountDto(
    Guid Id, DiscountType Type, decimal Value, decimal Amount, string Reason, ApprovalStatus Status, string? RequestedByName,
    DateTime RequestedAt, string? DecidedByName, DateTime? DecidedAt, string? DecisionNote);

public sealed record InvoiceSummaryDto(
    Guid Id, string InvoiceNumber, DateOnly InvoiceDate, Guid PatientId, string? PatientMRNumber, string? PatientName,
    InvoiceStatus Status, PaymentStatus PaymentStatus, string? SourceType, string? SourceReference, decimal Subtotal,
    decimal DiscountAmount, decimal NetAmount, decimal PaidAmount, decimal DueAmount, bool CancellationRequested);

public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    PatientBriefDto? Patient,
    InvoiceStatus Status,
    PaymentStatus PaymentStatus,
    string? SourceType,
    string? SourceReference,
    string? Notes,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal PaidAmount,
    decimal AdvanceAppliedAmount,
    decimal RefundedAmount,
    decimal DueAmount,
    bool CancellationRequested,
    bool IsRunningBill,
    DateTime? FinalizedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    DateTime CreatedAt,
    string? CreatedByName,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<DiscountDto> Discounts,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<RefundDto> Refunds,
    decimal PatientAdvanceBalance,
    uint RowVersion);

public sealed class InvoiceListQuery : PaginationRequest
{
    public Guid? PatientId { get; set; }
    public InvoiceStatus? Status { get; set; }

    /// <summary>Only finalized invoices with an amount due.</summary>
    public bool? DueOnly { get; set; }

    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

// ---------- Payments & refunds ----------

public sealed record PaymentCreateDto
{
    /// <summary>New for every payment attempt; resubmitting the same id returns the original receipt.</summary>
    public Guid ClientRequestId { get; init; }

    public Guid PatientId { get; init; }

    /// <summary>Null for an advance deposit.</summary>
    public Guid? InvoiceId { get; init; }

    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
}

public sealed record PaymentDto(
    Guid Id, string ReceiptNumber, Guid PatientId, Guid? InvoiceId, string? InvoiceNumber, decimal Amount, PaymentMethod Method,
    string? Reference, string? Notes, DateTime ReceivedAt, string? ReceivedByName);

/// <summary>Everything a printed money receipt shows.</summary>
public sealed record ReceiptDto(PaymentDto Payment, PatientBriefDto? Patient, InvoiceSummaryDto? Invoice, IReadOnlyList<InvoiceLineDto> Lines,
    decimal PatientAdvanceBalance);

public sealed record RefundCreateDto
{
    public Guid PatientId { get; init; }
    public RefundType Type { get; init; }

    /// <summary>Required for a cancellation refund.</summary>
    public Guid? InvoiceId { get; init; }

    /// <summary>Required for an advance return; a cancellation refund always returns all cash received.</summary>
    public decimal? Amount { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed record RefundPayDto
{
    public PaymentMethod Method { get; init; }
    public string? Reference { get; init; }
}

public sealed record RefundDto(
    Guid Id, string RefundNumber, Guid PatientId, string? PatientMRNumber, string? PatientName, Guid? InvoiceId, string? InvoiceNumber,
    RefundType Type, decimal Amount, string Reason, RefundStatus Status, string? RequestedByName, DateTime RequestedAt,
    string? DecidedByName, DateTime? DecidedAt, string? DecisionNote, string? PaidByName, DateTime? PaidAt, PaymentMethod? Method,
    string? Reference);

// ---------- Patient account, approvals, reports ----------

public sealed record PatientAccountDto(
    PatientBriefDto Patient, decimal OutstandingAmount, decimal AdvanceBalance, IReadOnlyList<InvoiceSummaryDto> Invoices,
    IReadOnlyList<PaymentDto> Payments, IReadOnlyList<RefundDto> Refunds);

public sealed record PendingDiscountDto(
    Guid DiscountId, Guid InvoiceId, string InvoiceNumber, string? PatientMRNumber, string? PatientName, decimal Subtotal,
    decimal DiscountAlreadyGiven, DiscountType Type, decimal Value, decimal Amount, decimal TotalPercentOfSubtotal, string Reason,
    string? RequestedByName, DateTime RequestedAt);

public sealed record ApprovalsDto(
    IReadOnlyList<PendingDiscountDto> Discounts, IReadOnlyList<RefundDto> RefundsToApprove, IReadOnlyList<RefundDto> RefundsToPay);

public sealed class CollectionQuery
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// <summary>Login id of a cashier. Users without Billing.Reports.View always see only their own collection.</summary>
    public Guid? CashierId { get; set; }
}

public sealed record CollectionMethodTotalDto(PaymentMethod Method, decimal Received, decimal Refunded, decimal Net);

public sealed record CollectionCashierTotalDto(Guid? UserId, string? Name, decimal Received, decimal Refunded, decimal Net);

/// <param name="Kind">Payment, Deposit or Refund.</param>
public sealed record CollectionTransactionDto(
    string Kind, Guid Id, string Number, DateTime At, string? PatientMRNumber, string? PatientName, string? InvoiceNumber,
    PaymentMethod Method, string? Reference, decimal Amount, string? UserName);

/// <summary>Money in and out by cashier and method. Advance applied to invoices moves no money and is reported separately.</summary>
public sealed record CollectionReportDto(
    DateOnly From, DateOnly To, Guid? CashierId, string? CashierName, bool AllCashiers, decimal TotalReceived, decimal TotalRefunded,
    decimal Net, decimal AdvanceApplied, IReadOnlyList<CollectionMethodTotalDto> ByMethod, IReadOnlyList<CollectionCashierTotalDto> ByCashier,
    IReadOnlyList<CollectionTransactionDto> Transactions);
