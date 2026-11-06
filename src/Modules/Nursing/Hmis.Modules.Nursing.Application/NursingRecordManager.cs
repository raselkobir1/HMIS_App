using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.Modules.Nursing.Domain.Entities;

namespace Hmis.Modules.Nursing.Application;

/// <summary>Intake/output charting and nursing notes (assessment, care plan, progress, incident).</summary>
public sealed class NursingRecordManager(
    IIntakeOutputRepository fluids,
    INursingNoteRepository notes,
    INursingUnitOfWork unitOfWork,
    NursingSupport support,
    IDateTimeProvider dateTime) : INursingRecordManager
{
    private static readonly TimeSpan MaxBackdate = TimeSpan.FromHours(24);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    // ---------- Intake / output ----------

    public async Task<IntakeOutputDayDto> GetIntakeOutputAsync(Guid admissionId, DateOnly? date, CancellationToken cancellationToken)
    {
        var stay = await support.GetViewableStayAsync(admissionId, cancellationToken);
        await support.AuditViewAsync("IntakeOutput", admissionId, cancellationToken);

        var day = date ?? NursingClock.FluidDayOf(dateTime.UtcNow, dateTime);
        var from = dateTime.StartOfLocalDayUtc(day).AddHours(8);
        var to = from.AddHours(24);
        var entries = await fluids.GetForAdmissionAsync(admissionId, from, to, cancellationToken);
        var names = await support.GetUserNamesAsync(entries.Select(e => e.CreatedBy), cancellationToken);
        var valid = entries.Where(e => !e.IsEnteredInError).ToList();

        var byShift = new[] { Shift.Morning, Shift.Evening, Shift.Night }.Select(shift =>
        {
            var (start, end) = NursingClock.ShiftWindow(day, shift, dateTime);
            return Totals(shift.ToString(), valid.Where(e => e.MeasuredAt >= start && e.MeasuredAt < end));
        }).ToList();

        return new IntakeOutputDayDto(await support.GetPatientAsync(stay, cancellationToken), day, from, to, Totals("24 hours", valid), byShift,
            entries.Select(e => ToDto(e, names)).ToList(), FluidCategories.Intake, FluidCategories.Output);
    }

    public async Task<IntakeOutputEntryDto> RecordIntakeOutputAsync(Guid admissionId, IntakeOutputCreateDto request, CancellationToken cancellationToken)
    {
        var stay = await support.GetActiveStayAsync(admissionId, cancellationToken);
        var measuredAt = CheckTime(request.MeasuredAt, "measuredAt");
        var entry = IntakeOutputEntry.Record(admissionId, stay.PatientId, measuredAt, request.Direction, request.Category, request.VolumeMl, request.Notes);
        await fluids.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entry, await support.GetUserNamesAsync([entry.CreatedBy], cancellationToken));
    }

    public async Task<IntakeOutputEntryDto> MarkIntakeOutputInErrorAsync(Guid id, EnteredInErrorDto request, CancellationToken cancellationToken)
    {
        var entry = await fluids.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Intake/output entry", id);
        if (entry.CreatedBy != support.UserId)
        {
            throw new ForbiddenException("Only the nurse who recorded this entry can mark it as entered in error.");
        }

        entry.MarkEnteredInError(request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entry, await support.GetUserNamesAsync([entry.CreatedBy], cancellationToken));
    }

    // ---------- Nursing notes ----------

    public async Task<NursingNotesDto> GetNotesAsync(Guid admissionId, CancellationToken cancellationToken)
    {
        var stay = await support.GetViewableStayAsync(admissionId, cancellationToken);
        await support.AuditViewAsync("NursingNotes", admissionId, cancellationToken);
        var list = await notes.GetForAdmissionAsync(admissionId, cancellationToken);
        var names = await support.GetUserNamesAsync(list.Select(n => n.CreatedBy), cancellationToken);
        return new NursingNotesDto(await support.GetPatientAsync(stay, cancellationToken), list.Select(n => ToDto(n, names)).ToList());
    }

    public async Task<NursingNoteDto> AddNoteAsync(Guid admissionId, NursingNoteCreateDto request, CancellationToken cancellationToken)
    {
        var stay = await support.GetActiveStayAsync(admissionId, cancellationToken);
        var note = NursingNote.Write(admissionId, stay.PatientId, request.Kind, request.Text);
        await notes.AddAsync(note, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(note, await support.GetUserNamesAsync([note.CreatedBy], cancellationToken));
    }

    public async Task<NursingNoteDto> MarkNoteInErrorAsync(Guid id, EnteredInErrorDto request, CancellationToken cancellationToken)
    {
        var note = await notes.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Nursing note", id);
        note.MarkEnteredInError(support.UserId, request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(note, await support.GetUserNamesAsync([note.CreatedBy], cancellationToken));
    }

    // ---------- helpers ----------

    private DateTime CheckTime(DateTime? requested, string field)
    {
        var now = dateTime.UtcNow;
        var value = requested is { } time ? NursingClock.AsUtc(time) : now;
        return value > now + ClockSkew || value < now - MaxBackdate
            ? throw new BusinessException(NursingErrorCodes.TimeInvalid, "The time must be within the last 24 hours and not in the future.", field)
            : value;
    }

    private static FluidTotalsDto Totals(string label, IEnumerable<IntakeOutputEntry> entries)
    {
        var list = entries.ToList();
        var intake = list.Where(e => e.Direction == FluidDirection.Intake).Sum(e => e.VolumeMl);
        var output = list.Where(e => e.Direction == FluidDirection.Output).Sum(e => e.VolumeMl);
        return new FluidTotalsDto(label, intake, output, intake - output);
    }

    private IntakeOutputEntryDto ToDto(IntakeOutputEntry e, IReadOnlyDictionary<Guid, string> names) =>
        new(e.Id, e.MeasuredAt, e.Direction, e.Category, e.VolumeMl, e.Notes, NursingSupport.NameOf(e.CreatedBy, names), e.IsEnteredInError,
            e.ErrorReason, e.CreatedBy == support.UserId);

    private NursingNoteDto ToDto(NursingNote n, IReadOnlyDictionary<Guid, string> names) =>
        new(n.Id, n.Kind, n.Text, NursingSupport.NameOf(n.CreatedBy, names), n.CreatedAt, n.IsEnteredInError, n.ErrorReason,
            n.CreatedBy == support.UserId);
}
