namespace Hmis.BuildingBlocks.Application.Security;

/// <summary>Evaluates a user's effective permissions. Implemented by the Identity module.</summary>
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);
}
