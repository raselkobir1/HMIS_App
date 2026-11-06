using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Contracts.Clinical;
using Hmis.Modules.Nursing.Domain.Entities;

namespace Hmis.Modules.Nursing.Application;

/// <summary>Ward shifts and the MAR dose schedule, in hospital time (Asia/Dhaka, no daylight saving).</summary>
public static class NursingClock
{
    /// <summary>A dose is "due" from an hour before to an hour after its time; after that it is overdue.</summary>
    public static readonly TimeSpan DueWindow = TimeSpan.FromHours(1);

    /// <summary>Doses may be recorded up to this long before their time (e.g. given early before a procedure).</summary>
    public static readonly TimeSpan EarlyLimit = TimeSpan.FromHours(2);

    private static readonly IReadOnlyDictionary<string, int[]> FixedHours = new Dictionary<string, int[]>
    {
        [MedicationFrequencies.Od] = [9],
        [MedicationFrequencies.Bd] = [9, 21],
        [MedicationFrequencies.Tds] = [8, 14, 22],
        [MedicationFrequencies.Qid] = [0, 6, 12, 18]
    };

    private static readonly IReadOnlyDictionary<string, int> IntervalHours = new Dictionary<string, int>
    {
        [MedicationFrequencies.Q4H] = 4,
        [MedicationFrequencies.Q6H] = 6,
        [MedicationFrequencies.Q8H] = 8,
        [MedicationFrequencies.Q12H] = 12
    };

    /// <summary>
    /// A time sent by an API client as UTC. A value without an offset (Kind Unspecified) is taken as UTC, never as the
    /// server's local time, which would shift it by the server's zone.
    /// </summary>
    public static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    public static (DateOnly Date, Shift Shift) ShiftAt(DateTime utc, IDateTimeProvider dateTime)
    {
        var date = dateTime.ToLocalDate(utc);
        var hour = (utc - dateTime.StartOfLocalDayUtc(date)).TotalHours;
        return hour switch
        {
            < 8 => (date.AddDays(-1), Shift.Night),
            < 14 => (date, Shift.Morning),
            < 20 => (date, Shift.Evening),
            _ => (date, Shift.Night)
        };
    }

    public static (DateTime StartUtc, DateTime EndUtc) ShiftWindow(DateOnly date, Shift shift, IDateTimeProvider dateTime)
    {
        var day = dateTime.StartOfLocalDayUtc(date);
        return shift switch
        {
            Shift.Morning => (day.AddHours(8), day.AddHours(14)),
            Shift.Evening => (day.AddHours(14), day.AddHours(20)),
            _ => (day.AddHours(20), day.AddHours(32))
        };
    }

    /// <summary>The fluid day (8am to 8am) that contains the instant.</summary>
    public static DateOnly FluidDayOf(DateTime utc, IDateTimeProvider dateTime)
    {
        var date = dateTime.ToLocalDate(utc);
        return utc < dateTime.StartOfLocalDayUtc(date).AddHours(8) ? date.AddDays(-1) : date;
    }

    /// <summary>
    /// Scheduled dose times of an order within [fromUtc, toUtc). Fixed-time frequencies use the hospital's standard times from the
    /// order start; interval frequencies repeat from the start; STAT is the start itself; PRN has no schedule. A dose is scheduled
    /// only before the order ends (stopped or completed).
    /// </summary>
    public static IReadOnlyList<DateTime> Schedule(MedicationOrderInfo order, DateTime fromUtc, DateTime toUtc, IDateTimeProvider dateTime)
    {
        var slots = new List<DateTime>();
        if (order.Frequency == MedicationFrequencies.Stat)
        {
            if (order.StartsAt >= fromUtc && order.StartsAt < toUtc)
            {
                slots.Add(order.StartsAt);
            }

            return slots;
        }

        var end = order.EndsAt is { } e && e < toUtc ? e : toUtc;
        var start = order.StartsAt > fromUtc ? order.StartsAt : fromUtc;
        if (start >= end)
        {
            return slots;
        }

        if (FixedHours.TryGetValue(order.Frequency, out var hours))
        {
            for (var date = dateTime.ToLocalDate(start); dateTime.StartOfLocalDayUtc(date) < end; date = date.AddDays(1))
            {
                var day = dateTime.StartOfLocalDayUtc(date);
                slots.AddRange(hours.Select(h => day.AddHours(h)).Where(t => t >= start && t < end));
            }
        }
        else if (IntervalHours.TryGetValue(order.Frequency, out var interval))
        {
            var step = TimeSpan.FromHours(interval);
            var skip = start > order.StartsAt ? (long)Math.Ceiling((start - order.StartsAt) / step) : 0;
            for (var t = order.StartsAt + step * skip; t < end; t += step)
            {
                slots.Add(t);
            }
        }

        return slots;
    }

    /// <summary>Not yet recorded: Due around its time, Overdue after, Upcoming before; Cancelled when a STAT order was stopped.</summary>
    public static string PendingState(MedicationOrderInfo order, DateTime slot, DateTime now)
    {
        if (order.Status == "Stopped" && order.Frequency == MedicationFrequencies.Stat)
        {
            return "Cancelled";
        }

        if (now > slot + DueWindow)
        {
            return "Overdue";
        }

        return now >= slot - DueWindow ? "Due" : "Upcoming";
    }
}
