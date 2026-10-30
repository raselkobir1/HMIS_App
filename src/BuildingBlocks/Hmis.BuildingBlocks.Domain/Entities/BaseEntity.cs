namespace Hmis.BuildingBlocks.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}
