using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.Modules.Ipd.Domain.Entities;

namespace Hmis.Modules.Ipd.Application;

/// <summary>One chargeable bed day and the instant whose bed sets its price.</summary>
public sealed record BedDay(DateOnly Date, DateTime PricedAt);

/// <summary>
/// Bed rent by calendar day with the noon rule (Q-IPD-1): the admission day always counts (priced at the bed admitted to);
/// each later day counts once 12:00 noon has passed while the patient is still in hospital (priced at the bed held at noon).
/// A patient discharged before noon is not charged for the discharge day.
/// </summary>
public static class BedDays
{
    public static IReadOnlyList<BedDay> For(DateTime admittedAt, DateTime until, IDateTimeProvider dateTime)
    {
        var first = dateTime.ToLocalDate(admittedAt);
        var last = dateTime.ToLocalDate(until);
        var days = new List<BedDay> { new(first, admittedAt) };
        for (var day = first.AddDays(1); day <= last; day = day.AddDays(1))
        {
            var noon = dateTime.StartOfLocalDayUtc(day).AddHours(12);
            if (until >= noon)
            {
                days.Add(new BedDay(day, noon));
            }
        }

        return days;
    }
}

/// <summary>
/// Posts bed rent to the stay's running bill. Safe to repeat: each day has its own charge key, so a day is billed once
/// (at the price when first posted). Used at admission, on transfer, by the periodic job and for the final bill.
/// </summary>
public sealed class BedChargePoster(
    IBedRepository beds,
    IBedCategoryRepository categories,
    IWardRepository wards,
    IChargeService charges,
    IDateTimeProvider dateTime)
{
    public const string KeyPrefix = "BED:";

    /// <param name="until">
    /// The cut-off for the final bill; otherwise the stay's own cut-off (the discharge order time once discharge is ordered,
    /// so a stay ordered out before noon is not charged for that day while the bill is prepared; otherwise now).
    /// </param>
    public async Task<BillStatus?> PostAsync(Admission admission, DateTime? until, CancellationToken cancellationToken)
    {
        // A finalized bill ends posting, except for a reopened stay: its later days go to a supplementary running bill.
        var bill = await charges.GetBillAsync(ChargeSources.IpdAdmission, admission.Id, cancellationToken);
        if (bill is not null && bill.Status != "Draft" && admission.ReopenedAt is null)
        {
            return bill;
        }

        // Days already on a finalized final bill (before a reopen) are never posted again.
        var days = BedDays.For(admission.AdmittedAt, until ?? admission.ChargeCutOff(dateTime.UtcNow), dateTime)
            .Where(d => admission.FinalBilledUntil is not { } billed || d.PricedAt > billed)
            .ToList();
        if (days.Count == 0)
        {
            return bill;
        }

        var bedIds = days.Select(d => admission.BedAt(d.PricedAt)).OfType<Guid>().Distinct().ToList();
        var bedMap = (await beds.GetByIdsAsync(bedIds, cancellationToken)).ToDictionary(b => b.Id);
        var categoryMap = (await categories.GetByIdsAsync(bedMap.Values.Select(b => b.CategoryId).Distinct().ToList(), cancellationToken))
            .ToDictionary(c => c.Id);
        var wardMap = (await wards.GetByIdsAsync(bedMap.Values.Select(b => b.WardId).Distinct().ToList(), cancellationToken)).ToDictionary(w => w.Id);

        var lines = new List<RunningChargeLine>();
        foreach (var day in days)
        {
            if (admission.BedAt(day.PricedAt) is not { } bedId || !bedMap.TryGetValue(bedId, out var bed))
            {
                continue;
            }

            var category = categoryMap.GetValueOrDefault(bed.CategoryId);
            var ward = wardMap.GetValueOrDefault(bed.WardId);
            lines.Add(new RunningChargeLine(
                $"{KeyPrefix}{day.Date:yyyy-MM-dd}",
                $"Bed rent {day.Date:dd MMM yyyy} — {ward?.Name} bed {bed.BedNumber} ({category?.Name})",
                ChargeCategories.Bed,
                1,
                category?.DailyRate ?? 0));
        }

        return await charges.AddToRunningBillAsync(
            new RunningChargeRequest(ChargeSources.IpdAdmission, admission.Id, admission.AdmissionNumber, admission.PatientId, lines),
            cancellationToken);
    }
}
