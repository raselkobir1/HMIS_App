using Hmis.BuildingBlocks.Application.Security;

namespace Hmis.Api.Services;

/// <summary>
/// Safe fallback used until the Identity module registers the real IPermissionChecker:
/// every permission-protected endpoint is denied.
/// </summary>
public sealed class DenyAllPermissionChecker : IPermissionChecker
{
    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
