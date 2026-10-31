using Hmis.BuildingBlocks.Application.Abstractions;

namespace Hmis.BuildingBlocks.Infrastructure.Services;

public sealed class DateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo HospitalTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HospitalTimeZone);

    public DateOnly LocalToday => DateOnly.FromDateTime(LocalNow);

    public DateTime StartOfLocalDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), HospitalTimeZone);
}
