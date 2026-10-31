using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Administration.Domain.Entities;

public enum DepartmentType
{
    Clinical,
    Diagnostic,
    Administrative,
    Support
}

/// <summary>
/// Department or sub-department (two levels: a sub-department's parent must be a top-level department).
/// Departments are deactivated, never deleted, because other modules reference them.
/// </summary>
[Audited]
public sealed class Department : AuditableEntity, IAggregateRoot, IHasConcurrencyToken, ISoftDeletable
{
    private Department() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public DepartmentType Type { get; private set; }
    public Guid? ParentDepartmentId { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint RowVersion { get; private set; }

    public static Department Create(string code, string name, DepartmentType type, Department? parent, string? description)
    {
        var department = new Department();
        department.Update(code, name, type, parent, description);
        return department;
    }

    public void Update(string code, string name, DepartmentType type, Department? parent, string? description)
    {
        if (parent is not null)
        {
            if (parent.Id == Id)
            {
                throw new DomainException("DEPARTMENT_SELF_PARENT", "A department cannot be its own parent.");
            }

            if (parent.ParentDepartmentId is not null)
            {
                throw new DomainException("DEPARTMENT_DEPTH", "A sub-department cannot have its own sub-departments.");
            }

            if (!parent.IsActive)
            {
                throw new DomainException("DEPARTMENT_PARENT_INACTIVE", "The parent department is inactive.");
            }
        }

        Code = NormalizeCode(code);
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Type = type;
        ParentDepartmentId = parent?.Id;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
