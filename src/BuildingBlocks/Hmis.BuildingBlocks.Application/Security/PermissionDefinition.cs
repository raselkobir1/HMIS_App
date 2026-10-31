namespace Hmis.BuildingBlocks.Application.Security;

/// <param name="Code">Permission code, e.g. "Patient.View".</param>
/// <param name="Module">Owning module, used for grouping in the UI.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="DefaultRoles">Roles that receive this permission when it is first created (seed only; admins can change later).</param>
/// <param name="RequiresExplicitGrant">
/// Clinical/sensitive permissions are NOT granted to the Super Administrator automatically (BRD §11, Q-SEC-1).
/// </param>
public sealed record PermissionDefinition(
    string Code,
    string Module,
    string Description,
    IReadOnlyCollection<string> DefaultRoles,
    bool RequiresExplicitGrant = false);

/// <summary>
/// Each module declares its permissions by implementing this; the Identity module syncs them into auth.Permissions at startup.
/// </summary>
public interface IPermissionDefinitionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}
