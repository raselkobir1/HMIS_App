using Microsoft.AspNetCore.Authorization;

namespace Hmis.BuildingBlocks.Presentation.Authorization;

/// <summary>Requires the caller to hold the given permission, e.g. [HasPermission("Patient.View")].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute(PermissionPolicy.PolicyPrefix + permission)
{
    public string Permission { get; } = permission;
}
