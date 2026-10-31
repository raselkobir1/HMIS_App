using FluentValidation;
using Hmis.BuildingBlocks.Application.Validators;
using Hmis.Modules.Identity.Application.DTOs.Requests;

namespace Hmis.Modules.Identity.Application.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserNameOrEmail).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(IdentityRules.PasswordMaxLength);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(IdentityRules.PasswordMaxLength);
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}

public sealed class UserCreateValidator : AbstractValidator<UserCreateDto>
{
    public UserCreateValidator()
    {
        RuleFor(x => x.UserName).ValidUserName();
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Email).ValidOptionalEmail().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneNumber).ValidOptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.RoleIds).NotNull().Must(ids => ids.Count <= 20).WithMessage("A user can have at most 20 roles.");
    }
}

public sealed class UserUpdateValidator : AbstractValidator<UserUpdateDto>
{
    public UserUpdateValidator()
    {
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Email).ValidOptionalEmail().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneNumber).ValidOptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}

public sealed class UserPasswordResetValidator : AbstractValidator<UserPasswordResetDto>
{
    public UserPasswordResetValidator() => RuleFor(x => x.NewPassword).StrongPassword();
}

public sealed class UserRolesUpdateValidator : AbstractValidator<UserRolesUpdateDto>
{
    public UserRolesUpdateValidator() =>
        RuleFor(x => x.RoleIds).NotNull().Must(ids => ids.Count <= 20).WithMessage("A user can have at most 20 roles.");
}

public sealed class UserPermissionsUpdateValidator : AbstractValidator<UserPermissionsUpdateDto>
{
    public UserPermissionsUpdateValidator() =>
        RuleFor(x => x.PermissionIds).NotNull().Must(ids => ids.Count <= 500);
}

public sealed class UserListQueryValidator : PaginationRequestValidator<UserListQuery>
{
    private static readonly string[] SortFields = ["userName", "fullName", "lastLoginAt", "createdAt"];

    public UserListQueryValidator() =>
        RuleFor(x => x.SortBy)
            .Must(s => SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrEmpty(x.SortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
}

public sealed class RoleCreateValidator : AbstractValidator<RoleCreateDto>
{
    public RoleCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).MaximumLength(250);
        RuleFor(x => x.PermissionIds).NotNull().Must(ids => ids.Count <= 500);
    }
}

public sealed class RoleUpdateValidator : AbstractValidator<RoleUpdateDto>
{
    public RoleUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public sealed class RolePermissionsUpdateValidator : AbstractValidator<RolePermissionsUpdateDto>
{
    public RolePermissionsUpdateValidator() =>
        RuleFor(x => x.PermissionIds).NotNull().Must(ids => ids.Count <= 500);
}

public sealed class RoleListQueryValidator : PaginationRequestValidator<RoleListQuery>
{
    public RoleListQueryValidator() =>
        RuleFor(x => x.SortBy)
            .Must(s => s is null || s.Equals("name", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortBy must be 'name'.");
}

public sealed class LoginHistoryQueryValidator : PaginationRequestValidator<LoginHistoryQuery>
{
    public LoginHistoryQueryValidator()
    {
        RuleFor(x => x.ToUtc)
            .GreaterThanOrEqualTo(x => x.FromUtc!.Value)
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue)
            .WithMessage("ToUtc must be on or after FromUtc.");
        RuleFor(x => x.SortBy).Empty().WithMessage("Login history is always sorted by most recent first.");
    }
}
