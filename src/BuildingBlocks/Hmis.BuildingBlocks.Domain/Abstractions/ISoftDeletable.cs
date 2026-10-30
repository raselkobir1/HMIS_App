namespace Hmis.BuildingBlocks.Domain.Abstractions;

/// <summary>Opt-in only: reference data that is deactivated rather than physically deleted.</summary>
public interface ISoftDeletable
{
    bool IsActive { get; }
}
