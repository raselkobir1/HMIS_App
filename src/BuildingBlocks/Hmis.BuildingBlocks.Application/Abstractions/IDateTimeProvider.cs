namespace Hmis.BuildingBlocks.Application.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    /// <summary>Current date in the hospital's timezone (Asia/Dhaka).</summary>
    DateOnly LocalToday { get; }

    /// <summary>Current wall-clock date and time in the hospital's timezone (Asia/Dhaka).</summary>
    DateTime LocalNow { get; }
}
