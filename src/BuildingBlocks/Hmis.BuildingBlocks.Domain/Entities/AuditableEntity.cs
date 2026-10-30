using Hmis.BuildingBlocks.Domain.Abstractions;

namespace Hmis.BuildingBlocks.Domain.Entities;

public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
}
