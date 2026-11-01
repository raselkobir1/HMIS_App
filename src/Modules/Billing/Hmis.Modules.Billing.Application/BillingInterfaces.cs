using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Billing.Domain.Entities;

namespace Hmis.Modules.Billing.Application;

public interface IBillingUnitOfWork : IUnitOfWork;

public interface IBillableServiceRepository : IRepository<BillableService>, IConcurrencyRepository<BillableService>
{
    Task<PagedResponse<BillableService>> SearchAsync(ServiceQuery query, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    /// <summary>Untracked.</summary>
    Task<BillableService?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<BillableService>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public interface IInvoiceRepository : IRepository<Invoice>, IConcurrencyRepository<Invoice>
{
    /// <summary>
    /// Serializes money operations for one patient (PostgreSQL transaction-scoped advisory lock), so balances, amounts due
    /// and advance cannot be spent twice by concurrent cashiers. Must be called inside a transaction.
    /// </summary>
    Task LockPatientAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>Untracked; null when the invoice does not exist.</summary>
    Task<Guid?> GetPatientIdAsync(Guid invoiceId, CancellationToken cancellationToken);

    /// <summary>Tracked, with lines and discounts.</summary>
    Task<Invoice?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Untracked.</summary>
    Task<Invoice?> GetByDiscountIdAsync(Guid discountId, CancellationToken cancellationToken);

    /// <summary>Untracked.</summary>
    Task<Invoice?> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken);

    Task<PagedResponse<Invoice>> SearchAsync(InvoiceListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<Invoice>> GetForPatientAsync(Guid patientId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<Invoice>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Total due on the patient's finalized invoices.</summary>
    Task<decimal> GetOutstandingAsync(Guid patientId, CancellationToken cancellationToken);

    Task<bool> HasDraftAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>Invoices (with discounts) that have a discount waiting for approval.</summary>
    Task<IReadOnlyList<Invoice>> GetWithPendingDiscountsAsync(CancellationToken cancellationToken);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByClientRequestAsync(Guid clientRequestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> GetForPatientAsync(Guid patientId, int take, CancellationToken cancellationToken);

    /// <summary>
    /// Advance deposits, less advance applied to invoices that are not cancelled, less advance returns that are
    /// requested, approved or paid.
    /// </summary>
    Task<decimal> GetAdvanceBalanceAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>Payments received in [fromUtc, toUtc), optionally by one user.</summary>
    Task<IReadOnlyList<Payment>> GetReceivedBetweenAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken cancellationToken);
}

public interface IRefundRepository : IRepository<Refund>, IConcurrencyRepository<Refund>
{
    /// <summary>Untracked; null when the refund does not exist.</summary>
    Task<Guid?> GetPatientIdAsync(Guid refundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Refund>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Refund>> GetForPatientAsync(Guid patientId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<Refund>> GetByStatusAsync(RefundStatus status, CancellationToken cancellationToken);

    /// <summary>Refunds paid out in [fromUtc, toUtc), optionally by one user.</summary>
    Task<IReadOnlyList<Refund>> GetPaidBetweenAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken cancellationToken);
}

public interface IServiceCatalogManager
{
    Task<PagedResponse<BillableServiceDto>> SearchAsync(ServiceQuery query, CancellationToken cancellationToken);

    Task<BillableServiceDto> CreateAsync(ServiceCreateDto request, CancellationToken cancellationToken);

    Task<BillableServiceDto> UpdateAsync(Guid id, ServiceUpdateDto request, CancellationToken cancellationToken);

    Task<BillableServiceDto> SetStatusAsync(Guid id, ServiceStatusDto request, CancellationToken cancellationToken);
}

public interface IInvoiceManager
{
    Task<PagedResponse<InvoiceSummaryDto>> SearchAsync(InvoiceListQuery query, CancellationToken cancellationToken);

    Task<InvoiceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<InvoiceDto> CreateAsync(InvoiceCreateDto request, CancellationToken cancellationToken);

    Task<InvoiceDto> UpdateAsync(Guid id, InvoiceUpdateDto request, CancellationToken cancellationToken);

    Task<InvoiceDto> FinalizeAsync(Guid id, RowVersionDto request, CancellationToken cancellationToken);

    Task<InvoiceDto> CancelAsync(Guid id, InvoiceCancelDto request, CancellationToken cancellationToken);

    Task<InvoiceDto> RequestDiscountAsync(Guid id, DiscountRequestDto request, CancellationToken cancellationToken);

    Task<InvoiceDto> ApproveDiscountAsync(Guid discountId, DecisionDto request, CancellationToken cancellationToken);

    Task<InvoiceDto> RejectDiscountAsync(Guid discountId, DecisionDto request, CancellationToken cancellationToken);
}

public interface IPaymentManager
{
    Task<ReceiptDto> CollectAsync(PaymentCreateDto request, CancellationToken cancellationToken);

    Task<ReceiptDto> GetReceiptAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<RefundDto> RequestRefundAsync(RefundCreateDto request, CancellationToken cancellationToken);

    Task<RefundDto> ApproveRefundAsync(Guid id, DecisionDto request, CancellationToken cancellationToken);

    Task<RefundDto> RejectRefundAsync(Guid id, DecisionDto request, CancellationToken cancellationToken);

    Task<RefundDto> PayRefundAsync(Guid id, RefundPayDto request, CancellationToken cancellationToken);

    Task<PatientAccountDto> GetAccountAsync(Guid patientId, CancellationToken cancellationToken);

    Task<ApprovalsDto> GetApprovalsAsync(CancellationToken cancellationToken);

    Task<CollectionReportDto> GetCollectionReportAsync(CollectionQuery query, CancellationToken cancellationToken);
}
