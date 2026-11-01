using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Appointment;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.BuildingBlocks.Contracts.Ipd;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Clinical.Domain.Entities;

namespace Hmis.Modules.Clinical.Application;

/// <summary>
/// OPD consultations and vitals. Access rules (BRD §11, ROLE-PERMISSION-MATRIX):
/// a doctor may view a patient's clinical records only with a treatment relationship (an appointment); other clinical
/// staff are governed by their permissions. Only the encounter's own doctor writes the consultation. Every view is audited.
/// </summary>
public sealed class ClinicalManager(
    IConsultationRepository consultations,
    IVitalSignRepository vitals,
    IClinicalUnitOfWork unitOfWork,
    IOpdEncounterService encounters,
    IInpatientLookup inpatients,
    IPatientLookup patientLookup,
    IPatientClinicalSummary clinicalSummary,
    IDoctorLookup doctorLookup,
    IUserEmployeeLookup userLookup,
    ICurrentUser currentUser,
    IAuditService auditService,
    IDateTimeProvider dateTime) : IClinicalManager
{
    private const int HistoryLimit = 50;

    // ---------- Workspace & vitals ----------

    public async Task<OpdWorkspaceDto> GetOpdWorkspaceAsync(Guid encounterId, CancellationToken cancellationToken)
    {
        var encounter = await GetEncounterAsync(encounterId, cancellationToken);
        var doctorId = await GetCurrentDoctorIdAsync(cancellationToken);
        await EnsureCanViewPatientAsync(doctorId, encounter.PatientId, cancellationToken);
        await AuditViewAsync("OpdEncounter", encounterId, cancellationToken);

        var patient = await GetPatientAsync(encounter.PatientId, cancellationToken);
        var summary = await clinicalSummary.GetAsync(encounter.PatientId, cancellationToken);
        var doctor = await doctorLookup.GetAsync(encounter.DoctorId, cancellationToken);
        var vitalList = await vitals.GetForEncounterAsync(EncounterType.Opd, encounterId, cancellationToken);
        var consultation = await consultations.GetByEncounterAsync(EncounterType.Opd, encounterId, cancellationToken);
        var previous = (await consultations.GetForPatientAsync(encounter.PatientId, HistoryLimit, cancellationToken))
            .Where(c => c.Id != consultation?.Id).ToList();

        var names = await GetUserNamesAsync(
            vitalList.Select(v => v.CreatedBy).Concat(consultation?.Addenda.Select(a => a.CreatedBy) ?? []), cancellationToken);
        var doctorNames = await GetDoctorNamesAsync(previous.Select(c => c.DoctorId), cancellationToken);

        return new OpdWorkspaceDto(
            new EncounterInfoDto(encounter.Id, encounter.Number, encounter.Date, encounter.SerialNumber, encounter.VisitType, encounter.Status),
            ToPatientInfo(patient),
            doctor is null ? null : ToDoctorInfo(doctor),
            summary.Allergies.Select(ToAlert).ToList(),
            summary.History.Select(h => new HistoryItemDto(h.Category, h.Description, h.OnsetDate)).ToList(),
            vitalList.Select(v => ToDto(v, names)).ToList(),
            consultation is null ? null : ToDto(consultation, names),
            previous.Select(c => ToSummary(c, doctorNames)).ToList(),
            doctorId == encounter.DoctorId);
    }

    public async Task<VitalSignDto> RecordOpdVitalsAsync(Guid encounterId, VitalSignRequestDto request, CancellationToken cancellationToken)
    {
        var encounter = await GetEncounterAsync(encounterId, cancellationToken);
        if (encounter.Status is not ("Waiting" or "InConsultation"))
        {
            throw new BusinessException(ClinicalErrorCodes.EncounterNotActive, "Vitals can be recorded only after check-in and before the visit is completed.");
        }

        var vital = VitalSign.Record(encounter.PatientId, EncounterType.Opd, encounterId, new VitalValues(
            request.Systolic, request.Diastolic, request.Pulse, request.TemperatureF, request.RespiratoryRate, request.SpO2,
            request.WeightKg, request.HeightCm, request.Notes));
        await vitals.AddAsync(vital, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(vital, await GetUserNamesAsync([vital.CreatedBy], cancellationToken));
    }

    public async Task<VitalSignDto> MarkVitalsInErrorAsync(Guid vitalSignId, EnteredInErrorDto request, CancellationToken cancellationToken)
    {
        var vital = await vitals.GetByIdAsync(vitalSignId, cancellationToken) ?? throw new NotFoundException("Vital signs", vitalSignId);
        vital.MarkEnteredInError(request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(vital, await GetUserNamesAsync([vital.CreatedBy], cancellationToken));
    }

    // ---------- Consultation ----------

    public async Task<ConsultationDto> OpenOpdConsultationAsync(Guid encounterId, CancellationToken cancellationToken)
    {
        var encounter = await GetEncounterAsync(encounterId, cancellationToken);
        var doctorId = await RequireCurrentDoctorIdAsync(cancellationToken);
        if (doctorId != encounter.DoctorId)
        {
            throw new ForbiddenException("Only the patient's own doctor can write this consultation.");
        }

        var consultation = await consultations.GetByEncounterAsync(EncounterType.Opd, encounterId, cancellationToken);
        if (consultation?.Status == ConsultationStatus.Finalized)
        {
            return await ToDtoAsync(consultation, cancellationToken);
        }

        if (consultation is null && encounter.Status is not ("Waiting" or "InConsultation"))
        {
            throw new BusinessException(ClinicalErrorCodes.EncounterNotActive,
                encounter.Status == "Completed"
                    ? "This visit was completed without a consultation record."
                    : "The patient has not been checked in yet.");
        }

        // Queue first (it enforces one consultation at a time per doctor); retrying is safe if the insert below fails.
        await encounters.StartAsync(encounterId, cancellationToken);

        if (consultation is null)
        {
            consultation = Consultation.Open(EncounterType.Opd, encounter.Id, encounter.Number, encounter.Date, encounter.PatientId, encounter.DoctorId);
            await consultations.AddAsync(consultation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await ToDtoAsync(consultation, cancellationToken);
    }

    public async Task<ConsultationRecordDto> GetConsultationAsync(Guid id, CancellationToken cancellationToken)
    {
        var consultation = await GetConsultationEntityAsync(id, cancellationToken);
        var doctorId = await GetCurrentDoctorIdAsync(cancellationToken);
        await EnsureCanViewPatientAsync(doctorId, consultation.PatientId, cancellationToken);
        await AuditViewAsync("Consultation", id, cancellationToken);

        var patient = await GetPatientAsync(consultation.PatientId, cancellationToken);
        var summary = await clinicalSummary.GetAsync(consultation.PatientId, cancellationToken);
        var doctor = await doctorLookup.GetAsync(consultation.DoctorId, cancellationToken);
        var latest = (await vitals.GetForEncounterAsync(consultation.EncounterType, consultation.EncounterId, cancellationToken))
            .Where(v => !v.IsEnteredInError).OrderByDescending(v => v.CreatedAt).FirstOrDefault();
        var names = await GetUserNamesAsync(consultation.Addenda.Select(a => a.CreatedBy).Append(latest?.CreatedBy), cancellationToken);

        return new ConsultationRecordDto(
            ToDto(consultation, names),
            ToPatientInfo(patient),
            doctor is null ? null : ToDoctorInfo(doctor),
            summary.Allergies.Select(ToAlert).ToList(),
            latest is null ? null : ToDto(latest, names),
            doctorId == consultation.DoctorId);
    }

    public async Task<ConsultationDto> SaveDraftAsync(Guid id, ConsultationSaveDto request, CancellationToken cancellationToken)
    {
        var consultation = await GetOwnConsultationAsync(id, cancellationToken);
        consultations.SetExpectedRowVersion(consultation, request.RowVersion);

        consultation.UpdateNotes(request.ChiefComplaint, request.HistoryOfPresentIllness, request.Examination, request.Advice,
            request.FollowUpDate, request.FollowUpInstructions, request.ReferralTo, request.ReferralReason);
        consultation.SetDiagnoses(request.Diagnoses.Select(d => new DiagnosisValues(d.Id, d.Type, d.Description)).ToList());
        consultation.SetPrescription(request.Prescription.Select(p => new PrescriptionValues(
            p.Id, p.MedicineName, p.Form, p.Strength, p.Route, p.DosePattern, p.Timing, p.DurationValue, p.DurationUnit, p.Quantity,
            p.Instructions)).ToList());
        consultation.SetInvestigations(request.Investigations.Select(i => new InvestigationValues(i.Id, i.Name, i.Notes)).ToList());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(consultation, cancellationToken);
    }

    public async Task<ConsultationDto> FinalizeAsync(Guid id, FinalizeDto request, CancellationToken cancellationToken)
    {
        var consultation = await GetOwnConsultationAsync(id, cancellationToken);
        if (consultation.Status == ConsultationStatus.Draft)
        {
            consultations.SetExpectedRowVersion(consultation, request.RowVersion);
            consultation.Finalize(dateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Separate module and transaction: if this fails, finalizing again only repeats this step.
        await encounters.CompleteAsync(consultation.EncounterId, cancellationToken);
        return await ToDtoAsync(consultation, cancellationToken);
    }

    public async Task<ConsultationDto> AddAddendumAsync(Guid id, AddendumCreateDto request, CancellationToken cancellationToken)
    {
        var consultation = await GetOwnConsultationAsync(id, cancellationToken);
        consultation.AddAddendum(request.Text);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(consultation, cancellationToken);
    }

    public async Task<IReadOnlyList<ConsultationSummaryDto>> GetPatientHistoryAsync(Guid patientId, CancellationToken cancellationToken)
    {
        await GetPatientAsync(patientId, cancellationToken);
        var doctorId = await GetCurrentDoctorIdAsync(cancellationToken);
        await EnsureCanViewPatientAsync(doctorId, patientId, cancellationToken);
        await AuditViewAsync("PatientClinicalHistory", patientId, cancellationToken);

        var list = await consultations.GetForPatientAsync(patientId, HistoryLimit, cancellationToken);
        var doctorNames = await GetDoctorNamesAsync(list.Select(c => c.DoctorId), cancellationToken);
        return list.Select(c => ToSummary(c, doctorNames)).ToList();
    }

    // ---------- access helpers ----------

    /// <summary>The signed-in user's doctor id (HR employee id), or null when the login is not a doctor.</summary>
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

    private async Task EnsureCanViewPatientAsync(Guid? doctorId, Guid patientId, CancellationToken cancellationToken)
    {
        if (doctorId is { } id && !await encounters.HasRelationshipAsync(id, patientId, cancellationToken)
            && !await inpatients.IsAttendingDoctorAsync(id, patientId, cancellationToken))
        {
            throw new ForbiddenException("You can view clinical records only of patients who have an appointment with you or were admitted under you.");
        }
    }

    private async Task<Consultation> GetOwnConsultationAsync(Guid id, CancellationToken cancellationToken)
    {
        var consultation = await GetConsultationEntityAsync(id, cancellationToken);
        if (await RequireCurrentDoctorIdAsync(cancellationToken) != consultation.DoctorId)
        {
            throw new ForbiddenException("Only the doctor who wrote this consultation can change it.");
        }

        return consultation;
    }

    private async Task<Consultation> GetConsultationEntityAsync(Guid id, CancellationToken cancellationToken) =>
        await consultations.GetWithLinesAsync(id, cancellationToken) ?? throw new NotFoundException("Consultation", id);

    private async Task<OpdEncounter> GetEncounterAsync(Guid encounterId, CancellationToken cancellationToken) =>
        await encounters.GetAsync(encounterId, cancellationToken) ?? throw new NotFoundException("OPD visit", encounterId);

    private async Task<PatientSummary> GetPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        await patientLookup.GetAsync(patientId, cancellationToken) ?? throw new NotFoundException("Patient", patientId);

    private Task AuditViewAsync(string entityName, Guid id, CancellationToken cancellationToken) =>
        auditService.RecordImmediateAsync(new AuditEvent("View", ClinicalPermissions.Module, entityName, id.ToString()), cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken) =>
        await userLookup.GetFullNamesAsync(userIds.OfType<Guid>().Distinct().ToList(), cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> GetDoctorNamesAsync(IEnumerable<Guid> doctorIds, CancellationToken cancellationToken) =>
        (await doctorLookup.GetManyAsync(doctorIds.Distinct().ToList(), cancellationToken)).ToDictionary(d => d.DoctorId, d => d.FullName);

    // ---------- mapping ----------

    private async Task<ConsultationDto> ToDtoAsync(Consultation c, CancellationToken cancellationToken) =>
        ToDto(c, await GetUserNamesAsync(c.Addenda.Select(a => a.CreatedBy), cancellationToken));

    private static ConsultationDto ToDto(Consultation c, IReadOnlyDictionary<Guid, string> names) =>
        new(c.Id, c.EncounterType, c.EncounterId, c.EncounterNumber, c.EncounterDate, c.PatientId, c.DoctorId, c.Status,
            c.ChiefComplaint, c.HistoryOfPresentIllness, c.Examination, c.Advice, c.FollowUpDate, c.FollowUpInstructions,
            c.ReferralTo, c.ReferralReason,
            c.Diagnoses.OrderBy(d => d.SortOrder).Select(d => new DiagnosisDto(d.Id, d.Type, d.Description)).ToList(),
            c.Prescription.OrderBy(p => p.SortOrder).Select(p => new PrescriptionItemDto(
                p.Id, p.MedicineName, p.Form, p.Strength, p.Route, p.DosePattern, p.Timing, p.DurationValue, p.DurationUnit, p.Quantity,
                p.Instructions)).ToList(),
            c.Investigations.OrderBy(i => i.SortOrder).Select(i => new InvestigationDto(i.Id, i.Name, i.Notes)).ToList(),
            c.Addenda.OrderBy(a => a.CreatedAt).Select(a => new AddendumDto(a.Id, a.Text, a.CreatedAt, NameOf(a.CreatedBy, names))).ToList(),
            c.FinalizedAt, c.RowVersion);

    private static VitalSignDto ToDto(VitalSign v, IReadOnlyDictionary<Guid, string> names) =>
        new(v.Id, v.CreatedAt, NameOf(v.CreatedBy, names), v.Systolic, v.Diastolic, v.Pulse, v.TemperatureF, v.RespiratoryRate, v.SpO2,
            v.WeightKg, v.HeightCm, v.Bmi, v.Notes, v.IsEnteredInError, v.ErrorReason);

    private static ConsultationSummaryDto ToSummary(Consultation c, IReadOnlyDictionary<Guid, string> doctorNames) =>
        new(c.Id, c.EncounterType, c.EncounterNumber, c.EncounterDate, c.DoctorId, doctorNames.GetValueOrDefault(c.DoctorId), c.Status,
            c.Diagnoses.FirstOrDefault(d => d.Type == DiagnosisType.Primary)?.Description, c.Prescription.Count);

    private static PatientInfoDto ToPatientInfo(PatientSummary p) =>
        new(p.Id, p.MRNumber, p.FullName, p.Gender, p.DateOfBirth, p.MobileNumber);

    private static DoctorInfoDto ToDoctorInfo(DoctorSummary d) =>
        new(d.DoctorId, d.FullName, d.Specialization, d.Qualifications, d.RegistrationNumber);

    private static AllergyAlertDto ToAlert(PatientAllergyAlert a) => new(a.Allergen, a.Type, a.Severity, a.Reaction);

    private static string? NameOf(Guid? userId, IReadOnlyDictionary<Guid, string> names) =>
        userId is { } id ? names.GetValueOrDefault(id) : null;
}
