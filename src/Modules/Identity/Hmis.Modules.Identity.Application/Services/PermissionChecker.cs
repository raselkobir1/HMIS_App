using Hmis.BuildingBlocks.Application.Security;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;

namespace Hmis.Modules.Identity.Application.Services;

/// <summary>
/// Request-scoped permission evaluation. Permissions are read from the database once per request
/// (never cached across requests), so revocations and deactivations take effect immediately.
/// </summary>
public sealed class PermissionChecker(IUserRepository userRepository) : IPermissionChecker
{
    private readonly Dictionary<Guid, IReadOnlySet<string>> _cache = [];

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue(userId, out var permissions))
        {
            var snapshot = await userRepository.GetAccessSnapshotAsync(userId, cancellationToken);

            // Inactive users and users who must change their password hold no permissions.
            permissions = snapshot is { IsActive: true, MustChangePassword: false }
                ? snapshot.PermissionCodes.ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>();

            _cache[userId] = permissions;
        }

        return permissions.Contains(permission);
    }
}
