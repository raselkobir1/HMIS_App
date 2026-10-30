namespace Hmis.BuildingBlocks.Domain.Abstractions;

/// <summary>Entities whose creation/modification user and time are stamped automatically on save.</summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
    Guid? ModifiedBy { get; set; }
}
