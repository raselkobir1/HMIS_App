using FluentValidation;
using Hmis.BuildingBlocks.Application.Validators;
using Hmis.Modules.Billing.Domain.Entities;

namespace Hmis.Modules.Billing.Application;

public sealed class ServiceCreateValidator : AbstractValidator<ServiceCreateDto>
{
    public ServiceCreateValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Use letters, digits and hyphens only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Price).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
    }
}

public sealed class ServiceUpdateValidator : AbstractValidator<ServiceUpdateDto>
{
    public ServiceUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Price).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
    }
}

public sealed class InvoiceLineInputValidator : AbstractValidator<InvoiceLineInputDto>
{
    public InvoiceLineInputValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty().WithMessage("Select a service.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.UnitPrice).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
    }
}

public sealed class InvoiceCreateValidator : AbstractValidator<InvoiceCreateDto>
{
    public InvoiceCreateValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).Must(l => l.Count <= 100).WithMessage("At most 100 lines.");
        RuleForEach(x => x.Lines).SetValidator(new InvoiceLineInputValidator());
    }
}

public sealed class InvoiceUpdateValidator : AbstractValidator<InvoiceUpdateDto>
{
    public InvoiceUpdateValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).Must(l => l.Count <= 100).WithMessage("At most 100 lines.");
        RuleForEach(x => x.Lines).SetValidator(new InvoiceLineInputValidator());
    }
}

public sealed class InvoiceCancelValidator : AbstractValidator<InvoiceCancelDto>
{
    public InvoiceCancelValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class DiscountRequestValidator : AbstractValidator<DiscountRequestDto>
{
    public DiscountRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0).PrecisionScale(12, 2, true);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Type == DiscountType.Percent).WithMessage("A percentage cannot exceed 100.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class DecisionValidator : AbstractValidator<DecisionDto>
{
    public DecisionValidator()
    {
        RuleFor(x => x.Note).MaximumLength(250);
    }
}

public sealed class PaymentCreateValidator : AbstractValidator<PaymentCreateDto>
{
    public PaymentCreateValidator()
    {
        RuleFor(x => x.ClientRequestId).NotEmpty();
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000m).PrecisionScale(12, 2, true);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Method).NotEqual(PaymentMethod.Advance).When(x => x.InvoiceId is null)
            .WithMessage("An advance deposit must be paid with cash, card, mobile banking or bank transfer.");
        RuleFor(x => x.Reference).NotEmpty().When(x => PaymentMethods.NeedsReference(x.Method))
            .WithMessage("Enter the transaction reference for this payment method.");
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(250);
    }
}

public sealed class RefundCreateValidator : AbstractValidator<RefundCreateDto>
{
    public RefundCreateValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.InvoiceId).NotEmpty().When(x => x.Type == RefundType.InvoiceCancellation).WithMessage("Select the invoice to cancel.");
        RuleFor(x => x.InvoiceId).Null().When(x => x.Type == RefundType.AdvanceReturn).WithMessage("An advance return is not linked to an invoice.");
        RuleFor(x => x.Amount).NotNull().GreaterThan(0).PrecisionScale(12, 2, true).When(x => x.Type == RefundType.AdvanceReturn);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class RefundPayValidator : AbstractValidator<RefundPayDto>
{
    public RefundPayValidator()
    {
        RuleFor(x => x.Method).IsInEnum().NotEqual(PaymentMethod.Advance).WithMessage("Choose how the money is returned.");
        RuleFor(x => x.Reference).NotEmpty().When(x => PaymentMethods.NeedsReference(x.Method))
            .WithMessage("Enter the transaction reference for this payment method.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class CollectionQueryValidator : AbstractValidator<CollectionQuery>
{
    public CollectionQueryValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty().GreaterThanOrEqualTo(x => x.From).WithMessage("The end date must not be before the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber < BillingPolicy.MaxReportDays)
            .WithName("to").WithMessage($"A report covers at most {BillingPolicy.MaxReportDays} days.");
    }
}

public sealed class InvoiceListQueryValidator : PaginationRequestValidator<InvoiceListQuery>
{
    public InvoiceListQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom!.Value).When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo must be on or after DateFrom.");
        RuleFor(x => x.SortBy).Empty().WithMessage("Invoices are sorted newest first.");
    }
}

public sealed class ServiceQueryValidator : PaginationRequestValidator<ServiceQuery>
{
    public ServiceQueryValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
    }
}
