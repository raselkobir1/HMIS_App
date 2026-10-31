using FluentValidation;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Application.Validators;

namespace Hmis.Modules.Audit.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class AuditApplicationMarker;

public static class AuditPermissions
{
    public const string Module = "Audit";
    public const string View = "Audit.View";
}

/// <summary>Audit access is Super Administrator only by default (docs/ROLE-PERMISSION-MATRIX.md).</summary>
public sealed class AuditPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(AuditPermissions.View, AuditPermissions.Module, "View the audit log", [])
    ];
}

public sealed class AuditLogQuery : PaginationRequest
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public Guid? UserId { get; set; }
    public string? Module { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
}

public sealed record AuditLogListItemDto(
    Guid Id,
    DateTime Timestamp,
    Guid? UserId,
    string? UserName,
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    string? IpAddress);

public sealed record AuditLogDetailDto(
    Guid Id,
    DateTime Timestamp,
    Guid? UserId,
    string? UserName,
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    string? IpAddress,
    string? TraceId,
    string? OldValues,
    string? NewValues);

public sealed record AuditFilterOptionsDto(IReadOnlyList<string> Modules, IReadOnlyList<string> EntityNames, IReadOnlyList<string> Actions);

public sealed class AuditLogQueryValidator : PaginationRequestValidator<AuditLogQuery>
{
    public AuditLogQueryValidator()
    {
        RuleFor(x => x.ToUtc)
            .GreaterThanOrEqualTo(x => x.FromUtc!.Value)
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue)
            .WithMessage("ToUtc must be on or after FromUtc.");
        RuleFor(x => x.Module).MaximumLength(50);
        RuleFor(x => x.EntityName).MaximumLength(100);
        RuleFor(x => x.EntityId).MaximumLength(200);
        RuleFor(x => x.Action).MaximumLength(50);
        RuleFor(x => x.SortBy).Empty().WithMessage("The audit log is always sorted by most recent first.");
    }
}
