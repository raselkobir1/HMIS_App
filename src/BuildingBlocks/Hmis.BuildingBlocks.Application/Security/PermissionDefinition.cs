namespace Hmis.BuildingBlocks.Application.Security;

public sealed record PermissionDefinition(string Code, string Module, string Description);

/// <summary>
/// Each module declares its permissions by implementing this; the Identity module syncs them into auth.Permissions at startup.
/// </summary>
public interface IPermissionDefinitionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}
