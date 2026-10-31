using Hmis.BuildingBlocks.Domain.Entities;

namespace Hmis.Modules.Identity.Domain.Entities;

/// <summary>Permission catalogue entry. Synced from module code at startup; never created by users.</summary>
public sealed class Permission : BaseEntity
{
    private Permission() { }

    public string Code { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool RequiresExplicitGrant { get; private set; }

    public static Permission Create(string code, string module, string description, bool requiresExplicitGrant) =>
        new() { Code = code, Module = module, Description = description, RequiresExplicitGrant = requiresExplicitGrant };

    public void UpdateMetadata(string module, string description, bool requiresExplicitGrant)
    {
        Module = module;
        Description = description;
        RequiresExplicitGrant = requiresExplicitGrant;
    }
}
