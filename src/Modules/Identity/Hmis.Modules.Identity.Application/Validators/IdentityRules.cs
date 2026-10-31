using FluentValidation;

namespace Hmis.Modules.Identity.Application.Validators;

/// <summary>Reusable rules (password policy Q-SEC-2, username, contact fields).</summary>
public static class IdentityRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(PasswordMinLength)
            .MaximumLength(PasswordMaxLength)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

    public static IRuleBuilderOptions<T, string> ValidUserName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Length(3, 50)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("Username may contain only letters, digits, '.', '_' and '-'.");

    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(150);

    public static IRuleBuilderOptions<T, string?> ValidOptionalEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(256).EmailAddress();

    public static IRuleBuilderOptions<T, string?> ValidOptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(20)
            .Matches(@"^\+?[0-9\s-]{6,20}$").WithMessage("Phone number is not valid.");
}
