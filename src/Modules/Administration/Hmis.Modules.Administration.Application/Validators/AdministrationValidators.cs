using FluentValidation;
using Hmis.BuildingBlocks.Application.Validators;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Domain.Entities;

namespace Hmis.Modules.Administration.Application.Validators;

public sealed class HospitalProfileUpdateValidator : AbstractValidator<HospitalProfileUpdateDto>
{
    public HospitalProfileUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameBangla).MaximumLength(200);
        RuleFor(x => x.ShortName).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Website).MaximumLength(256)
            .Must(BeHttpUrl).When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("Website must be a valid http(s) URL.");
        RuleFor(x => x.RegistrationNumber).MaximumLength(100);
        RuleFor(x => x.LicenseNumber).MaximumLength(100);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.OpeningHours).MaximumLength(500);
    }

    private static bool BeHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public sealed class SystemSettingsUpdateValidator : AbstractValidator<SystemSettingsUpdateDto>
{
    public SystemSettingsUpdateValidator()
    {
        RuleFor(x => x.CurrencyCode).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Currency code must be a 3-letter ISO code.");
        RuleFor(x => x.CurrencySymbol).NotEmpty().MaximumLength(5);
        RuleFor(x => x.FinancialYearStartMonth).InclusiveBetween(1, 12);
    }
}

public sealed class DepartmentCreateValidator : AbstractValidator<DepartmentCreateDto>
{
    public DepartmentCreateValidator()
    {
        RuleFor(x => x.Code).DepartmentCode();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class DepartmentUpdateValidator : AbstractValidator<DepartmentUpdateDto>
{
    public DepartmentUpdateValidator()
    {
        RuleFor(x => x.Code).DepartmentCode();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class DepartmentListQueryValidator : PaginationRequestValidator<DepartmentListQuery>
{
    private static readonly string[] SortFields = ["code", "name", "type"];

    public DepartmentListQueryValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.SortBy)
            .Must(s => SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrEmpty(x.SortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
    }
}

public sealed class DesignationCreateValidator : AbstractValidator<DesignationCreateDto>
{
    public DesignationCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).IsInEnum();
    }
}

public sealed class DesignationUpdateValidator : AbstractValidator<DesignationUpdateDto>
{
    public DesignationUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).IsInEnum();
    }
}

public sealed class DesignationListQueryValidator : PaginationRequestValidator<DesignationListQuery>
{
    private static readonly string[] SortFields = ["name", "category"];

    public DesignationListQueryValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.SortBy)
            .Must(s => SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrEmpty(x.SortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
    }
}

public sealed class NumberSequenceUpdateValidator : AbstractValidator<NumberSequenceUpdateDto>
{
    public NumberSequenceUpdateValidator()
    {
        RuleFor(x => x.Prefix).MaximumLength(10).Matches("^[A-Za-z0-9-]*$")
            .WithMessage("Prefix may contain only letters, digits and '-'.");
        RuleFor(x => x.YearFormat).IsInEnum();
        RuleFor(x => x.PadLength).InclusiveBetween(NumberSequence.MinPadLength, NumberSequence.MaxPadLength);
        RuleFor(x => x.ResetPolicy).IsInEnum();
    }
}

public sealed class NumberSequenceNextValueValidator : AbstractValidator<NumberSequenceNextValueDto>
{
    public NumberSequenceNextValueValidator() => RuleFor(x => x.NextValue).GreaterThan(0);
}

internal static class AdministrationRules
{
    public static IRuleBuilderOptions<T, string> DepartmentCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(2, 20).Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Code may contain only letters, digits, '_' and '-'.");
}
