using FluentValidation;
using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Validators;
using Hmis.Modules.HR.Domain.Entities;

namespace Hmis.Modules.HR.Application;

public sealed class EmployeeCreateValidator : AbstractValidator<EmployeeCreateDto>
{
    public EmployeeCreateValidator(IDateTimeProvider dateTime)
    {
        var today = dateTime.LocalToday;
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.DateOfBirth).LessThan(today).GreaterThan(new DateOnly(1900, 1, 1)).When(x => x.DateOfBirth.HasValue);
        RuleFor(x => x.MobileNumber).EmployeeMobile();
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.DesignationId).NotEmpty();
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.JoiningDate).JoiningDate(today);
    }
}

public sealed class EmployeeUpdateValidator : AbstractValidator<EmployeeUpdateDto>
{
    public EmployeeUpdateValidator(IDateTimeProvider dateTime)
    {
        var today = dateTime.LocalToday;
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.DateOfBirth).LessThan(today).GreaterThan(new DateOnly(1900, 1, 1)).When(x => x.DateOfBirth.HasValue);
        RuleFor(x => x.MobileNumber).EmployeeMobile();
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.DesignationId).NotEmpty();
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.JoiningDate).JoiningDate(today);
    }
}

public sealed class EmployeeStatusUpdateValidator : AbstractValidator<EmployeeStatusUpdateDto>
{
    public EmployeeStatusUpdateValidator(IDateTimeProvider dateTime)
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ExitDate)
            .LessThanOrEqualTo(dateTime.LocalToday.AddYears(1))
            .When(x => x.ExitDate.HasValue)
            .WithMessage("Exit date cannot be more than a year ahead.");
    }
}

public sealed class EmployeeListQueryValidator : PaginationRequestValidator<EmployeeListQuery>
{
    private static readonly string[] SortFields = ["employeeCode", "fullName", "joiningDate"];

    public EmployeeListQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.SortBy)
            .Must(s => SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrEmpty(x.SortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
    }
}

public sealed class EmployeeLookupQueryValidator : AbstractValidator<EmployeeLookupQuery>
{
    public EmployeeLookupQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Take).InclusiveBetween(1, 50);
    }
}

internal static class HrRules
{
    public static IRuleBuilderOptions<T, string> EmployeeMobile<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(20).Matches(@"^\+?[0-9\s-]{6,20}$").WithMessage("Mobile number is not valid.")
            .Must(m => m.Count(char.IsDigit) >= Employee.MinMobileDigits)
            .WithMessage($"Mobile number needs at least {Employee.MinMobileDigits} digits.");

    /// <summary>Joining dates up to a year ahead are allowed so new hires can be registered before they start.</summary>
    public static IRuleBuilderOptions<T, DateOnly> JoiningDate<T>(this IRuleBuilder<T, DateOnly> rule, DateOnly today) =>
        rule.NotEmpty()
            .GreaterThan(new DateOnly(1950, 1, 1))
            .LessThanOrEqualTo(today.AddYears(1)).WithMessage("Joining date cannot be more than a year ahead.");
}
