namespace Hmis.BuildingBlocks.Application.Abstractions;

/// <summary>
/// For aggregates with optimistic concurrency: tells persistence which row version the client last saw,
/// so SaveChanges fails with a conflict (HTTP 409) if someone else changed the record meanwhile.
/// </summary>
public interface IConcurrencyRepository<in T> where T : class
{
    void SetExpectedRowVersion(T entity, uint rowVersion);
}
