namespace Hmis.BuildingBlocks.Application.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    /// <summary>Current date in the hospital's timezone (Asia/Dhaka).</summary>
    DateOnly LocalToday { get; }

    /// <summary>Current wall-clock date and time in the hospital's timezone (Asia/Dhaka).</summary>
    DateTime LocalNow { get; }

    /// <summary>The UTC instant at which a hospital-local date begins (for "from date / to date" filters on UTC timestamps).</summary>
    DateTime StartOfLocalDayUtc(DateOnly date);

    /// <summary>The hospital-local calendar date of a UTC instant.</summary>
    DateOnly ToLocalDate(DateTime utc);
}
