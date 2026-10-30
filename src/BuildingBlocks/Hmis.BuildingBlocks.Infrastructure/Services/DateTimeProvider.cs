using Hmis.BuildingBlocks.Application.Abstractions;

namespace Hmis.BuildingBlocks.Infrastructure.Services;

public sealed class DateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo HospitalTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly LocalToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HospitalTimeZone));
}
