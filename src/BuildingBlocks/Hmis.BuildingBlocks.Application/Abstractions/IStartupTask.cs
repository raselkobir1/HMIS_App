namespace Hmis.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Work a module runs once when the host starts, after the database has been migrated
/// (e.g. reference-data seeding). Executed in ascending <see cref="Order"/>.
/// </summary>
public interface IStartupTask
{
    int Order { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
