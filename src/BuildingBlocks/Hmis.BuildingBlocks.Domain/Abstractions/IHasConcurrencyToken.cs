namespace Hmis.BuildingBlocks.Domain.Abstractions;

/// <summary>Mapped to PostgreSQL xmin; mismatches on update raise a concurrency conflict (HTTP 409).</summary>
public interface IHasConcurrencyToken
{
    uint RowVersion { get; }
}
