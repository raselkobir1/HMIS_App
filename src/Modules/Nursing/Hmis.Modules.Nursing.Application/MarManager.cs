using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Clinical;
using Hmis.Modules.Nursing.Domain.Entities;

namespace Hmis.Modules.Nursing.Application;

/// <summary>
/// Medication Administration Record. Every entry is linked to a doctor's medication order (BRD Module 07); a scheduled dose
/// can be recorded once (a unique index backs this), and only for a time the order actually schedules.
/// </summary>
public sealed class MarManager(
    IMedicationAdministrationRepository administrations,
    INursingUnitOfWork unitOfWork,
    IMedicationOrderLookup orderLookup,
    NursingSupport support,
    IDateTimeProvider dateTime) : IMarManager
{
    private static readonly TimeSpan MaxBackdate = TimeSpan.FromHours(24);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public async Task<MarDto> GetAsync(Guid admissionId, DateOnly? date, CancellationToken cancellationToken)
    {
        var stay = await support.GetViewableStayAsync(admissionId, cancellationToken);
        await support.AuditViewAsync("MedicationAdministrationRecord", admissionId, cancellationToken);

        var day = date ?? dateTime.LocalToday;
        var from = dateTime.StartOfLocalDayUtc(day);
        var to = dateTime.StartOfLocalDayUtc(day.AddDays(1));
        var now = dateTime.UtcNow;
        var orders = await orderLookup.GetForAdmissionsAsync([admissionId], cancellationToken);
        var entries = await administrations.GetForAdmissionsAsync([admissionId], from, to, cancellationToken);
        var names = await support.GetUserNamesAsync(entries.Select(e => e.CreatedBy), cancellationToken);
        var doctors = await support.GetDoctorNamesAsync(orders.Select(o => o.OrderedByDoctorId), cancellationToken);

        var list = new List<MarOrderDto>();
        foreach (var order in orders)
        {
            var slots = NursingClock.Schedule(order, from, to, dateTime);
            var orderEntries = entries.Where(e => e.MedicationOrderId == order.Id).OrderBy(e => e.OccurredAt).ToList();
            var activeThatDay = order.StartsAt < to && (order.EndsAt is null || order.EndsAt >= from);
            if (slots.Count == 0 && orderEntries.Count == 0 && !(activeThatDay && order.Frequency == MedicationFrequencies.Prn))
            {
                continue;
            }

            var slotDtos = slots.Select(slot =>
            {
                var entry = orderEntries.FirstOrDefault(e => !e.IsEnteredInError && e.ScheduledAt == slot);
                return new DoseSlotDto(slot, entry?.Status.ToString() ?? NursingClock.PendingState(order, slot, now),
                    entry is null ? null : ToDto(entry, names));
            }).ToList();

            list.Add(new MarOrderDto(order.Id, order.MedicineName, order.Strength, order.Form, order.Route, order.Dose, order.Frequency,
                order.StartsAt, order.EndsAt, order.Status, order.PrnIndication, order.Instructions, doctors.GetValueOrDefault(order.OrderedByDoctorId),
                slotDtos, orderEntries.Select(e => ToDto(e, names)).ToList()));
        }

        return new MarDto(await support.GetPatientAsync(stay, cancellationToken), day,
            list.OrderBy(o => o.Status == "Active" ? 0 : 1).ThenBy(o => o.MedicineName).ToList());
    }

    public async Task<AdministrationDto> RecordAsync(Guid admissionId, AdministrationCreateDto request, CancellationToken cancellationToken)
    {
        var stay = await support.GetActiveStayAsync(admissionId, cancellationToken);
        var order = await orderLookup.GetAsync(request.OrderId, cancellationToken);
        if (order is null || order.AdmissionId != admissionId)
        {
            throw new BusinessException(NursingErrorCodes.OrderInvalid, "Select a medication ordered for this patient.", "orderId");
        }

        var now = dateTime.UtcNow;
        var occurredAt = request.OccurredAt is { } occurred ? NursingClock.AsUtc(occurred) : now;
        if (occurredAt > now + ClockSkew || occurredAt < now - MaxBackdate)
        {
            throw new BusinessException(NursingErrorCodes.TimeInvalid, "The time must be within the last 24 hours and not in the future.", "occurredAt");
        }

        DateTime? scheduledAt = null;
        if (order.Frequency == MedicationFrequencies.Prn)
        {
            if (request.ScheduledAt is not null)
            {
                throw new BusinessException(NursingErrorCodes.NotScheduled, "A PRN medicine has no scheduled times.", "scheduledAt");
            }

            // Times entered by hand have minute precision: a dose given the minute the order was signed is valid.
            if (occurredAt < order.StartsAt - ClockSkew || (order.EndsAt is { } end && occurredAt >= end))
            {
                throw new BusinessException(NursingErrorCodes.OrderNotActive, "The order was not active at that time.", "occurredAt");
            }
        }
        else
        {
            scheduledAt = await GetScheduledSlotAsync(order, request.ScheduledAt, now, cancellationToken);
        }

        var entry = MedicationAdministration.Record(order.Id, admissionId, stay.PatientId, scheduledAt,
            new AdministrationValues(request.Status, occurredAt,
                request.Status == AdministrationStatus.Given ? request.DoseGiven ?? order.Dose : null, request.Reason, request.Notes));
        await administrations.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entry, await support.GetUserNamesAsync([entry.CreatedBy], cancellationToken));
    }

    public async Task<AdministrationDto> MarkInErrorAsync(Guid id, EnteredInErrorDto request, CancellationToken cancellationToken)
    {
        var entry = await administrations.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("MAR entry", id);
        entry.MarkEnteredInError(support.UserId, request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entry, await support.GetUserNamesAsync([entry.CreatedBy], cancellationToken));
    }

    /// <summary>The order's own dose time matching the request (to the second); not too early and not already recorded.</summary>
    private async Task<DateTime> GetScheduledSlotAsync(MedicationOrderInfo order, DateTime? requested, DateTime now, CancellationToken cancellationToken)
    {
        if (requested is not { } value)
        {
            throw new BusinessException(NursingErrorCodes.NotScheduled, "Select the scheduled dose time.", "scheduledAt");
        }

        var utc = NursingClock.AsUtc(value);
        var slot = NursingClock.Schedule(order, utc.AddSeconds(-1), utc.AddSeconds(1), dateTime).Cast<DateTime?>().FirstOrDefault()
            ?? throw new BusinessException(NursingErrorCodes.NotScheduled, "That is not a scheduled time for this order.", "scheduledAt");

        if (order.Status == "Stopped" && order.Frequency == MedicationFrequencies.Stat)
        {
            throw new BusinessException(NursingErrorCodes.OrderNotActive, "The doctor cancelled this STAT order.");
        }

        if (slot > now + NursingClock.EarlyLimit)
        {
            throw new BusinessException(NursingErrorCodes.TooEarly, "This dose is not due yet; it can be recorded up to 2 hours early.", "scheduledAt");
        }

        if (await administrations.IsRecordedAsync(order.Id, slot, cancellationToken))
        {
            throw new ConflictException("This dose has already been recorded. Refresh the MAR.", NursingErrorCodes.AlreadyRecorded);
        }

        return slot;
    }

    private AdministrationDto ToDto(MedicationAdministration e, IReadOnlyDictionary<Guid, string> names) =>
        new(e.Id, e.MedicationOrderId, e.ScheduledAt, e.Status, e.OccurredAt, e.DoseGiven, e.Reason, e.Notes, NursingSupport.NameOf(e.CreatedBy, names),
            e.CreatedAt, e.IsEnteredInError, e.ErrorReason, e.CreatedBy == support.UserId);
}
