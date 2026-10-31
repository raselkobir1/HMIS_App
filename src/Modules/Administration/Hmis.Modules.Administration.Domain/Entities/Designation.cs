using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;

namespace Hmis.Modules.Administration.Domain.Entities;

public enum DesignationCategory
{
    Doctor,
    Nurse,
    Technician,
    Pharmacist,
    Administrative,
    Support,
    Other
}

/// <summary>Job title used by HR (e.g. "Consultant", "Staff Nurse"). Deactivated, never deleted.</summary>
[Audited]
public sealed class Designation : AuditableEntity, IAggregateRoot, IHasConcurrencyToken, ISoftDeletable
{
    private Designation() { }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public DesignationCategory Category { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint RowVersion { get; private set; }

    public static Designation Create(string name, DesignationCategory category)
    {
        var designation = new Designation();
        designation.Update(name, category);
        return designation;
    }

    public void Update(string name, DesignationCategory category)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Category = category;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
