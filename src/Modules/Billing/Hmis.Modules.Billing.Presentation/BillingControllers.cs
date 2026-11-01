using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Billing.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Billing.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class BillingPresentationMarker;

[Route("api/v{version:apiVersion}/billing/services")]
public sealed class BillingServicesController(IServiceCatalogManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<PagedResponse<BillableServiceDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ServiceQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    [HttpPost]
    [HasPermission(BillingPermissions.ServicesManage)]
    [ProducesResponseType<ApiResponse<BillableServiceDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ServiceCreateDto request, CancellationToken cancellationToken) =>
        CreatedResponse(nameof(Search), new { version = "1", search = request.Code }, await manager.CreateAsync(request, cancellationToken),
            "Service added.");

    [HttpPut("{id:guid}")]
    [HasPermission(BillingPermissions.ServicesManage)]
    [ProducesResponseType<ApiResponse<BillableServiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ServiceUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(id, request, cancellationToken), "Service updated.");

    [HttpPatch("{id:guid}/status")]
    [HasPermission(BillingPermissions.ServicesManage)]
    [ProducesResponseType<ApiResponse<BillableServiceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] ServiceStatusDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetStatusAsync(id, request, cancellationToken), request.IsActive ? "Service activated." : "Service deactivated.");
}

[Route("api/v{version:apiVersion}/billing/invoices")]
public sealed class InvoicesController(IInvoiceManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<PagedResponse<InvoiceSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] InvoiceListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a draft invoice from price-list services.</summary>
    [HttpPost]
    [HasPermission(BillingPermissions.InvoiceManage)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] InvoiceCreateDto request, CancellationToken cancellationToken)
    {
        var invoice = await manager.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = invoice.Id, version = "1" }, invoice, "Draft invoice created.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(BillingPermissions.InvoiceManage)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] InvoiceUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(id, request, cancellationToken), "Draft saved.");

    [HttpPost("{id:guid}/finalize")]
    [HasPermission(BillingPermissions.InvoiceManage)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> FinalizeInvoice(Guid id, [FromBody] RowVersionDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.FinalizeAsync(id, request, cancellationToken), "Invoice finalized.");

    /// <summary>Discards a draft (Billing.Invoice.Manage) or cancels a finalized invoice with no cash received (Billing.Invoice.Cancel).</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] InvoiceCancelDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CancelAsync(id, request, cancellationToken), "Invoice cancelled.");

    /// <summary>Applies at once within the discount limit; larger discounts wait for approval.</summary>
    [HttpPost("{id:guid}/discounts")]
    [HasPermission(BillingPermissions.DiscountApply)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestDiscount(Guid id, [FromBody] DiscountRequestDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.RequestDiscountAsync(id, request, cancellationToken), "Discount recorded.");
}

[Route("api/v{version:apiVersion}/billing/discounts")]
public sealed class DiscountsController(IInvoiceManager manager) : ApiControllerBase
{
    [HttpPost("{id:guid}/approve")]
    [HasPermission(BillingPermissions.DiscountApprove)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecisionDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.ApproveDiscountAsync(id, request, cancellationToken), "Discount approved.");

    [HttpPost("{id:guid}/reject")]
    [HasPermission(BillingPermissions.DiscountApprove)]
    [ProducesResponseType<ApiResponse<InvoiceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecisionDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.RejectDiscountAsync(id, request, cancellationToken), "Discount rejected.");
}

[Route("api/v{version:apiVersion}/billing/payments")]
public sealed class PaymentsController(IPaymentManager manager) : ApiControllerBase
{
    /// <summary>Receives a payment against an invoice, or an advance deposit when invoiceId is empty. Idempotent per clientRequestId.</summary>
    [HttpPost]
    [HasPermission(BillingPermissions.PaymentCollect)]
    [ProducesResponseType<ApiResponse<ReceiptDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Collect([FromBody] PaymentCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CollectAsync(request, cancellationToken), "Payment received.");

    /// <summary>Data for the printed money receipt.</summary>
    [HttpGet("{id:guid}/receipt")]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<ReceiptDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetReceiptAsync(id, cancellationToken));
}

[Route("api/v{version:apiVersion}/billing/refunds")]
public sealed class RefundsController(IPaymentManager manager) : ApiControllerBase
{
    [HttpPost]
    [HasPermission(BillingPermissions.RefundRequest)]
    [ProducesResponseType<ApiResponse<RefundDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestRefund([FromBody] RefundCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.RequestRefundAsync(request, cancellationToken), "Refund requested.");

    [HttpPost("{id:guid}/approve")]
    [HasPermission(BillingPermissions.RefundApprove)]
    [ProducesResponseType<ApiResponse<RefundDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecisionDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.ApproveRefundAsync(id, request, cancellationToken), "Refund approved.");

    [HttpPost("{id:guid}/reject")]
    [HasPermission(BillingPermissions.RefundApprove)]
    [ProducesResponseType<ApiResponse<RefundDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecisionDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.RejectRefundAsync(id, request, cancellationToken), "Refund rejected.");

    /// <summary>Records that the approved refund was handed to the patient.</summary>
    [HttpPost("{id:guid}/pay")]
    [HasPermission(BillingPermissions.RefundRequest)]
    [ProducesResponseType<ApiResponse<RefundDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Pay(Guid id, [FromBody] RefundPayDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.PayRefundAsync(id, request, cancellationToken), "Refund paid.");
}

[Route("api/v{version:apiVersion}/billing")]
public sealed class BillingOverviewController(IPaymentManager manager) : ApiControllerBase
{
    /// <summary>Outstanding amount, advance balance, invoices, payments and refunds of one patient.</summary>
    [HttpGet("patients/{patientId:guid}/account")]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<PatientAccountDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccount(Guid patientId, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetAccountAsync(patientId, cancellationToken));

    /// <summary>Discounts and refunds waiting for approval, and approved refunds waiting to be paid out.</summary>
    [HttpGet("approvals")]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<ApprovalsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApprovals(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetApprovalsAsync(cancellationToken));

    /// <summary>Collection by cashier and payment method. Without Billing.Reports.View a user sees only their own collection.</summary>
    [HttpGet("reports/collections")]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<CollectionReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCollections([FromQuery] CollectionQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetCollectionReportAsync(query, cancellationToken));
}

/// <summary>Discount approval limit and payment methods in use (read by every billing screen).</summary>
[Route("api/v{version:apiVersion}/billing/settings")]
public sealed class BillingSettingsController(IBillingSettingsManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(BillingPermissions.View)]
    [ProducesResponseType<ApiResponse<BillingSettingsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetAsync(cancellationToken));

    [HttpPut]
    [HasPermission(BillingPermissions.SettingsManage)]
    [ProducesResponseType<ApiResponse<BillingSettingsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update([FromBody] BillingSettingsUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(request, cancellationToken), "Billing settings saved.");
}
