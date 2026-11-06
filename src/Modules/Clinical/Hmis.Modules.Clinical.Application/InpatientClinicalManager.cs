using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Appointment;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.BuildingBlocks.Contracts.Clinical;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.BuildingBlocks.Contracts.Ipd;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Clinical.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hmis.Modules.Clinical.Application;

/// <summary>
/// The doctor's side of an inpatient stay: progress notes, medication orders and ward vitals. Any doctor may write
/// (duty and on-call doctors cover the wards), always signed with their own doctor profile. A doctor may read the chart
/// while the patient is in hospital; afterwards only with a treatment relationship (attending or an OPD visit).
/// Records are written only while the patient is still admitted. Every chart view is audited.
/// </summary>
public sealed class InpatientClinicalManager(
    IProgressNoteRepository notes,
    IMedicationOrderRepository orders,
    IVitalSignRepository vitals,
    IClinicalUnitOfWork unitOfWork,
    IInpatientLookup inpatients,
    IOpdEncounterService encounters,
    IPatientLookup patientLookup,
    IPatientClinicalSummary clinicalSummary,
    IDoctorLookup doctorLookup,
    IUserEmployeeLookup userLookup,
    ICurrentUser currentUser,
    IAuditService auditService,
    IDateTimeProvider dateTime,
    IChargeService charges,
    ILogger<InpatientClinicalManager> logger) : IInpatientClinicalManager, IMedicationOrderLookup
{
    private static readonly TimeSpan MaxBackdatedStart = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxFutureStart = TimeSpan.FromDays(7);

    public async Task<InpatientChartDto> GetChartAsync(Guid admissionId, CancellationToken cancellationToken)
    {
        var stay = await GetStayAsync(admissionId, cancellationToken);
        var doctorId = await GetCurrentDoctorIdAsync(cancellationToken);
        var noteList = await notes.GetForAdmissionAsync(admissionId, cancellationToken);
        var orderList = await orders.GetForAdmissionsAsync([admissionId], cancellationToken);
        await EnsureCanViewAsync(doctorId, stay, noteList, orderList, cancellationToken);
        await auditService.RecordImmediateAsync(
            new AuditEvent("View", ClinicalPermissions.Module, "InpatientChart", admissionId.ToString()), cancellationToken);

        var patient = await patientLookup.GetAsync(stay.PatientId, cancellationToken) ?? throw new NotFoundException("Patient", stay.PatientId);
        var summary = await clinicalSummary.GetAsync(stay.PatientId, cancellationToken);
        var vitalList = await vitals.GetForEncounterAsync(EncounterType.Ipd, admissionId, cancellationToken);

        var doctorNames = await GetDoctorNamesAsync(
            noteList.Select(n => n.DoctorId)
                .Concat(orderList.SelectMany(o => new[] { o.OrderedByDoctorId, o.StoppedByDoctorId ?? Guid.Empty }))
                .Append(stay.AttendingDoctorId),
            cancellationToken);
        var userNames = await userLookup.GetFullNamesAsync(vitalList.Select(v => v.CreatedBy).OfType<Guid>().Distinct().ToList(), cancellationToken);
        var now = dateTime.UtcNow;

        return new InpatientChartDto(
            ToStayDto(stay, doctorNames),
            new PatientInfoDto(patient.Id, patient.MRNumber, patient.FullName, patient.Gender, patient.DateOfBirth, patient.MobileNumber),
            summary.Allergies.Select(a => new AllergyAlertDto(a.Allergen, a.Type, a.Severity, a.Reaction)).ToList(),
            noteList.Select(n => ToDto(n, doctorNames)).ToList(),
            orderList.OrderByDescending(o => o.CreatedAt).Select(o => ToDto(o, doctorNames, now)).ToList(),
            vitalList.Select(v => ToDto(v, userNames)).ToList(),
            doctorId is not null,
            doctorId);
    }

    // ---------- Progress notes ----------

    public async Task<ProgressNoteDto> AddProgressNoteAsync(Guid admissionId, ProgressNoteCreateDto request, CancellationToken cancellationToken)
    {
        var stay = await GetActiveStayAsync(admissionId, cancellationToken);
        var doctorId = await RequireCurrentDoctorIdAsync(cancellationToken);
        var note = ProgressNote.Write(stay.AdmissionId, stay.PatientId, doctorId,
            new ProgressNoteValues(request.Kind, request.Subjective, request.Objective, request.Assessment, request.Plan));
        await notes.AddAsync(note, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (request.Kind is ProgressNoteKind.WardRound or ProgressNoteKind.ConsultantReview)
        {
            await PostVisitFeeSafelyAsync(stay, doctorId, cancellationToken);
        }

        return ToDto(note, await GetDoctorNamesAsync([doctorId], cancellationToken));
    }

    /// <summary>
    /// The doctor's inpatient visit fee (HR doctor profile), once per doctor per hospital day on the stay's running bill.
    /// Skipped when the fee is 0 or the final bill is already prepared; a failure is logged and never undoes the note.
    /// </summary>
    private async Task PostVisitFeeSafelyAsync(InpatientStay stay, Guid doctorId, CancellationToken cancellationToken)
    {
        try
        {
            if (await doctorLookup.GetAsync(doctorId, cancellationToken) is not { InpatientVisitFee: > 0 } doctor
                || await charges.GetBillAsync(ChargeSources.IpdAdmission, stay.AdmissionId, cancellationToken) is { Status: not "Draft" })
            {
                return;
            }

            var day = dateTime.LocalToday;
            await charges.AddToRunningBillAsync(new RunningChargeRequest(ChargeSources.IpdAdmission, stay.AdmissionId, stay.AdmissionNumber,
                stay.PatientId,
                [new RunningChargeLine($"VISIT:{doctorId:N}:{day:yyyy-MM-dd}", $"Doctor visit {day:dd MMM yyyy} — {doctor.FullName}",
                    ChargeCategories.Consultation, 1, doctor.InpatientVisitFee)]), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Visit fee for admission {AdmissionNumber} could not be posted", stay.AdmissionNumber);
        }
    }

    public async Task<ProgressNoteDto> MarkProgressNoteInErrorAsync(Guid noteId, EnteredInErrorDto request, CancellationToken cancellationToken)
    {
        var note = await notes.GetByIdAsync(noteId, cancellationToken) ?? throw new NotFoundException("Progress note", noteId);
        note.MarkEnteredInError(await RequireCurrentDoctorIdAsync(cancellationToken), request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(note, await GetDoctorNamesAsync([note.DoctorId], cancellationToken));
    }

    // ---------- Medication orders ----------

    public async Task<MedicationOrderDto> CreateOrderAsync(Guid admissionId, MedicationOrderCreateDto request, CancellationToken cancellationToken)
    {
        var stay = await GetActiveStayAsync(admissionId, cancellationToken);
        var doctorId = await RequireCurrentDoctorIdAsync(cancellationToken);
        var now = dateTime.UtcNow;
        // A start without an offset (Kind Unspecified) is UTC from the API client, never the server's local time.
        var startsAt = request.StartsAt is { } requested
            ? requested.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(requested, DateTimeKind.Utc) : requested.ToUniversalTime()
            : now;
        if (startsAt < now - MaxBackdatedStart || startsAt > now + MaxFutureStart)
        {
            throw new BusinessException(ClinicalErrorCodes.MedicationStart, "The start time must be within the last hour or the next 7 days.", "startsAt");
        }

        var order = MedicationOrder.Create(stay.AdmissionId, stay.PatientId, doctorId, new MedicationOrderValues(
            request.MedicineName, request.Strength, request.Form, request.Route, request.Dose, request.Frequency, startsAt, request.DurationDays,
            request.PrnIndication, request.Instructions));
        await orders.AddAsync(order, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(order, await GetDoctorNamesAsync([doctorId], cancellationToken), now);
    }

    public async Task<MedicationOrderDto> StopOrderAsync(Guid orderId, MedicationOrderStopDto request, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(orderId, cancellationToken) ?? throw new NotFoundException("Medication order", orderId);
        orders.SetExpectedRowVersion(order, request.RowVersion);
        var doctorId = await RequireCurrentDoctorIdAsync(cancellationToken);
        var now = dateTime.UtcNow;
        order.Stop(doctorId, request.Reason, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(order, await GetDoctorNamesAsync([order.OrderedByDoctorId, doctorId], cancellationToken), now);
    }

    // ---------- Vitals ----------

    public async Task<VitalSignDto> RecordVitalsAsync(Guid admissionId, VitalSignRequestDto request, CancellationToken cancellationToken)
    {
        var stay = await GetActiveStayAsync(admissionId, cancellationToken);
        var vital = VitalSign.Record(stay.PatientId, EncounterType.Ipd, stay.AdmissionId, new VitalValues(
            request.Systolic, request.Diastolic, request.Pulse, request.TemperatureF, request.RespiratoryRate, request.SpO2,
            request.WeightKg, request.HeightCm, request.Notes));
        await vitals.AddAsync(vital, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var names = await userLookup.GetFullNamesAsync(vital.CreatedBy is { } id ? [id] : [], cancellationToken);
        return ToDto(vital, names);
    }

    // ---------- IMedicationOrderLookup (for the Nursing MAR) ----------

    async Task<MedicationOrderInfo?> IMedicationOrderLookup.GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        await orders.GetByIdAsync(orderId, cancellationToken) is { } order ? ToInfo(order, dateTime.UtcNow) : null;

    async Task<IReadOnlyList<MedicationOrderInfo>> IMedicationOrderLookup.GetForAdmissionsAsync(
        IReadOnlyCollection<Guid> admissionIds, CancellationToken cancellationToken)
    {
        var now = dateTime.UtcNow;
        return (await orders.GetForAdmissionsAsync(admissionIds, cancellationToken)).Select(o => ToInfo(o, now)).ToList();
    }

    // ---------- helpers ----------

    private async Task<InpatientStay> GetStayAsync(Guid admissionId, CancellationToken cancellationToken) =>
        await inpatients.GetAsync(admissionId, cancellationToken) ?? throw new NotFoundException("Admission", admissionId);

    private async Task<InpatientStay> GetActiveStayAsync(Guid admissionId, CancellationToken cancellationToken)
    {
        var stay = await GetStayAsync(admissionId, cancellationToken);
        return stay.IsActive
            ? stay
            : throw new BusinessException(ClinicalErrorCodes.StayNotActive, "The patient has been discharged; the inpatient chart is closed.");
    }

    /// <summary>
    /// Q-NUR-5: while the patient is in hospital any holder of Clinical.View may open the chart. After discharge it is closed
    /// to nurses and other staff; a doctor may still open it with a treatment relationship: attending doctor, a duty doctor
    /// who wrote a note or ordered medicines on this stay, or an OPD visit with the patient.
    /// </summary>
    private async Task EnsureCanViewAsync(Guid? doctorId, InpatientStay stay, IReadOnlyList<ProgressNote> stayNotes,
        IReadOnlyList<MedicationOrder> stayOrders, CancellationToken cancellationToken)
    {
        if (stay.IsActive)
        {
            return;
        }

        if (doctorId is not { } id)
        {
            throw new ForbiddenException("The patient has been discharged; the chart is closed. Only the doctors who treated the patient can open it.");
        }

        var treated = stayNotes.Any(n => n.DoctorId == id)
                      || stayOrders.Any(o => o.OrderedByDoctorId == id || o.StoppedByDoctorId == id)
                      || await inpatients.IsAttendingDoctorAsync(id, stay.PatientId, cancellationToken)
                      || await encounters.HasRelationshipAsync(id, stay.PatientId, cancellationToken);
        if (!treated)
        {
            throw new ForbiddenException("After discharge, a doctor can view the chart only of patients they treated.");
        }
    }

    private async Task<Guid?> GetCurrentDoctorIdAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var employeeId = await userLookup.GetEmployeeIdAsync(userId, cancellationToken);
        return employeeId is { } id && await doctorLookup.GetAsync(id, cancellationToken) is not null ? id : null;
    }

    private async Task<Guid> RequireCurrentDoctorIdAsync(CancellationToken cancellationToken) =>
        await GetCurrentDoctorIdAsync(cancellationToken)
        ?? throw new BusinessException(ClinicalErrorCodes.NotLinkedToDoctor,
            "Your login is not linked to a doctor profile. Ask an administrator to link it to your employee record.");

    private async Task<IReadOnlyDictionary<Guid, string>> GetDoctorNamesAsync(IEnumerable<Guid> doctorIds, CancellationToken cancellationToken) =>
        (await doctorLookup.GetManyAsync(doctorIds.Where(id => id != Guid.Empty).Distinct().ToList(), cancellationToken))
        .ToDictionary(d => d.DoctorId, d => d.FullName);

    // ---------- mapping ----------

    private static InpatientStayDto ToStayDto(InpatientStay s, IReadOnlyDictionary<Guid, string> doctorNames) =>
        new(s.AdmissionId, s.AdmissionNumber, s.Status, s.IsActive, s.AdmittedAt, s.ReleasedAt, s.WardName, s.BedNumber, s.AttendingDoctorId,
            doctorNames.GetValueOrDefault(s.AttendingDoctorId));

    private static ProgressNoteDto ToDto(ProgressNote n, IReadOnlyDictionary<Guid, string> doctorNames) =>
        new(n.Id, n.Kind, n.Subjective, n.Objective, n.Assessment, n.Plan, n.DoctorId, doctorNames.GetValueOrDefault(n.DoctorId), n.CreatedAt,
            n.IsEnteredInError, n.ErrorReason);

    private static MedicationOrderDto ToDto(MedicationOrder o, IReadOnlyDictionary<Guid, string> doctorNames, DateTime now) =>
        new(o.Id, o.MedicineName, o.Strength, o.Form, o.Route, o.Dose, o.Frequency, o.StartsAt, o.DurationDays, o.EndsAt, o.StatusAt(now),
            o.PrnIndication, o.Instructions, doctorNames.GetValueOrDefault(o.OrderedByDoctorId), o.CreatedAt, o.StoppedAt,
            o.StoppedByDoctorId is { } stoppedBy ? doctorNames.GetValueOrDefault(stoppedBy) : null, o.StopReason, o.RowVersion);

    private static MedicationOrderInfo ToInfo(MedicationOrder o, DateTime now) =>
        new(o.Id, o.AdmissionId, o.PatientId, o.MedicineName, o.Strength, o.Form, o.Route, o.Dose, o.Frequency, o.StartsAt, o.EndsAt,
            o.StatusAt(now), o.PrnIndication, o.Instructions, o.OrderedByDoctorId, o.CreatedAt);

    private static VitalSignDto ToDto(VitalSign v, IReadOnlyDictionary<Guid, string> names) =>
        new(v.Id, v.CreatedAt, v.CreatedBy is { } id ? names.GetValueOrDefault(id) : null, v.Systolic, v.Diastolic, v.Pulse, v.TemperatureF,
            v.RespiratoryRate, v.SpO2, v.WeightKg, v.HeightCm, v.Bmi, v.Notes, v.IsEnteredInError, v.ErrorReason);
}
