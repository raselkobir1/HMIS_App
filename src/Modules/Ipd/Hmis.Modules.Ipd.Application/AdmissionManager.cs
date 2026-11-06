using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.BuildingBlocks.Contracts.Notifications;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Ipd.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hmis.Modules.Ipd.Application;

/// <summary>
/// Admission, bed transfer, discharge and release. Billing never blocks clinical decisions (BRD Module 05 safety rule):
/// the doctor's discharge order is always accepted; only the final release needs a settled bill or an administrator's
/// override. Bed rent is posted to the stay's running bill in Billing (D18).
/// </summary>
public sealed class AdmissionManager(
    IAdmissionRepository admissions,
    IBedRepository beds,
    IBedCategoryRepository categories,
    IWardRepository wards,
    IIpdUnitOfWork unitOfWork,
    BedChargePoster chargePoster,
    IChargeService charges,
    IPatientBalanceLookup balances,
    IPatientLookup patientLookup,
    IDoctorLookup doctorLookup,
    IDepartmentLookup departmentLookup,
    IUserEmployeeLookup userLookup,
    INumberSequenceService numberSequences,
    IPermissionChecker permissions,
    IAuditService auditService,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<AdmissionManager> logger,
    IServicePriceLookup prices,
    IPatientStatusService patientStatus,
    INotificationService notifications) : IAdmissionManager
{
    // ---------- Queries ----------

    public async Task<PagedResponse<AdmissionSummaryDto>> SearchAsync(AdmissionListQuery query, CancellationToken cancellationToken)
    {
        query.PatientIds = query.PatientId is { } patientId ? await patientLookup.GetWithMergedAsync(patientId, cancellationToken) : null;
        var page = await admissions.SearchAsync(query, cancellationToken);
        var context = await LoadContextAsync(page.Items, cancellationToken);
        return page.Map(a => ToSummary(a, context));
    }

    public async Task<AdmissionDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDtoAsync(await GetAdmissionAsync(id, cancellationToken), cancellationToken);

    /// <summary>Clinical content: requires clinical access; every view is audited.</summary>
    public async Task<DischargeSummaryDto> GetDischargeSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var admission = await GetAdmissionAsync(id, cancellationToken);
        if (!await CanViewClinicalAsync(cancellationToken))
        {
            throw new ForbiddenException("Only clinical staff can view the discharge summary.");
        }

        if (admission.Status == AdmissionStatus.Admitted || admission.FinalDiagnosis is null)
        {
            throw new BusinessException("DISCHARGE_NOT_ORDERED", "The doctor has not written the discharge summary yet.");
        }

        await auditService.RecordImmediateAsync(new AuditEvent("View", IpdPermissions.Module, "DischargeSummary", id.ToString()), cancellationToken);
        var context = await LoadContextAsync([admission], cancellationToken);
        var patient = context.Patients.GetValueOrDefault(admission.PatientId);
        var doctor = context.Doctors.GetValueOrDefault(admission.AttendingDoctorId);
        var current = admission.Beds.OrderBy(b => b.StartedAt).Last();
        var bed = context.Beds.GetValueOrDefault(current.BedId);
        return new DischargeSummaryDto(
            admission.AdmissionNumber,
            patient is null ? null : new PatientInfoDto(patient.Id, patient.MRNumber, patient.FullName, patient.Gender, patient.DateOfBirth, patient.MobileNumber),
            doctor is null ? null : new DoctorInfoDto(doctor.DoctorId, doctor.FullName, doctor.Qualifications, doctor.Specialization, doctor.RegistrationNumber),
            await DepartmentNameAsync(admission.DepartmentId, cancellationToken),
            bed is null ? null : context.Wards.GetValueOrDefault(bed.WardId)?.Name,
            bed?.BedNumber,
            admission.AdmittedAt,
            admission.DischargedAt,
            admission.ReleasedAt,
            admission.Reason,
            (await DischargeInfoAsync(admission, cancellationToken))!);
    }

    // ---------- Admission ----------

    public async Task<AdmissionDto> AdmitAsync(AdmitDto request, CancellationToken cancellationToken)
    {
        var patient = await patientLookup.GetAsync(request.PatientId, cancellationToken);
        if (patient is not { Status: "Active" })
        {
            throw new BusinessException(IpdErrorCodes.PatientUnavailable,
                patient?.Status switch
                {
                    "Merged" => "This patient record was merged into another record. Admit the surviving record.",
                    "Deceased" => "This patient is recorded as deceased.",
                    _ => "Select an active patient."
                },
                "patientId");
        }

        if (await admissions.HasActiveAsync(request.PatientId, cancellationToken))
        {
            throw new ConflictException("This patient is already admitted. Open the current admission instead.", IpdErrorCodes.AlreadyAdmitted);
        }

        var doctor = await GetCurrentDoctorAsync(request.AttendingDoctorId, "attendingDoctorId", cancellationToken);
        var bed = await GetAssignableBedAsync(request.BedId, cancellationToken);

        // Allocated outside the save (Administration's own atomic statement); a failed save leaves a gap, never a duplicate.
        var number = await numberSequences.NextAsync(IpdNumberSequences.Admission, cancellationToken);
        var admission = Admission.Admit(number, request.PatientId, doctor.DoctorId, doctor.DepartmentId, request.Source, request.SourceReference,
            request.Reason, new EmergencyContactValues(request.EmergencyContactName, request.EmergencyContactRelation, request.EmergencyContactPhone),
            bed, dateTime.UtcNow);

        // The bed's row version and the unique "one active stay per bed / per patient" indexes stop double booking.
        await admissions.AddAsync(admission, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await PostAdmissionFeeSafelyAsync(admission, cancellationToken);
        await PostChargesSafelyAsync(admission, cancellationToken);
        var dto = await ToDtoAsync(admission, cancellationToken);
        await notifications.NotifyEmployeeAsync(admission.AttendingDoctorId, new NotificationMessage(NotificationTypes.PatientAdmitted,
            $"Patient admitted under you · {dto.Summary.WardName} bed {dto.Summary.BedNumber}",
            $"{dto.Summary.PatientName} ({dto.Summary.PatientMRNumber}) — {admission.Reason}", $"/ipd/admissions/{admission.Id}"), cancellationToken);
        return dto;
    }

    public async Task<AdmissionDto> TransferAsync(Guid id, TransferDto request, CancellationToken cancellationToken)
    {
        var admission = await GetAdmissionAsync(id, cancellationToken);
        admissions.SetExpectedRowVersion(admission, request.RowVersion);
        var current = await beds.GetByIdAsync(admission.CurrentBed?.BedId ?? Guid.Empty, cancellationToken)
            ?? throw new BusinessException(IpdErrorCodes.BedInvalid, "The patient has no current bed.");
        var target = await GetAssignableBedAsync(request.BedId, cancellationToken);

        // Bed days already passed are posted at the old bed's price before the move.
        await PostChargesSafelyAsync(admission, cancellationToken);
        admission.TransferBed(current, target, request.Reason, dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(admission, cancellationToken);
    }

    public async Task<AdmissionDto> ChangeDoctorAsync(Guid id, ChangeDoctorDto request, CancellationToken cancellationToken)
    {
        var admission = await GetAdmissionAsync(id, cancellationToken);
        admissions.SetExpectedRowVersion(admission, request.RowVersion);
        var doctor = await GetCurrentDoctorAsync(request.DoctorId, "doctorId", cancellationToken);
        admission.ChangeAttendingDoctor(doctor.DoctorId, doctor.DepartmentId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(admission, cancellationToken);
    }

    public async Task<AdmissionDto> SyncChargesAsync(Guid id, CancellationToken cancellationToken)
    {
        var admission = await GetAdmissionAsync(id, cancellationToken);
        if (admission.Status is AdmissionStatus.Admitted or AdmissionStatus.DischargeOrdered)
        {
            await chargePoster.PostAsync(admission, null, cancellationToken);
        }

        return await ToDtoAsync(admission, cancellationToken);
    }

    // ---------- Discharge ----------

    /// <summary>The doctor's order with the discharge summary; never blocked by billing. Can be amended until the patient leaves.</summary>
    public async Task<AdmissionDto> OrderDischargeAsync(Guid id, DischargeOrderDto request, CancellationToken cancellationToken)
    {
        var admission = await GetAdmissionAsync(id, cancellationToken);
        admissions.SetExpectedRowVersion(admission, request.RowVersion);
        if (request.FollowUpDate is { } followUp && followUp <= dateTime.LocalToday)
        {
            throw new BusinessException(IpdErrorCodes.FollowUpDate, "The follow-up date must be in the future.", "followUpDate");
        }

        var doctorId = await GetSignedInDoctorIdAsync(cancellationToken);
        var firstOrder = admission.Status == AdmissionStatus.Admitted;
        admission.OrderDischarge(doctorId, new DischargeValues(request.Type, request.FinalDiagnosis, request.HospitalCourse,
            request.DischargeMedications, request.Advice, request.FollowUpDate), dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var dto = await ToDtoAsync(admission, cancellationToken);
        if (firstOrder)
        {
            await notifications.NotifyPermissionAsync(IpdPermissions.DischargeComplete, new NotificationMessage(NotificationTypes.DischargeOrdered,
                $"Discharge ordered · {dto.Summary.WardName} bed {dto.Summary.BedNumber}",
                $"{dto.Summary.PatientName} ({dto.Summary.PatientMRNumber}): prepare the final bill.", $"/ipd/admissions/{admission.Id}"), cancellationToken);
        }

        return dto;
    }

    public async Task<AdmissionDto> CancelDischargeOrderAsync(Guid id, AdmissionRowVersionDto request, CancellationToken cancellationToken)
    {
        var admission = await GetAdmissionAsync(id, cancellationToken);
        admissions.SetExpectedRowVersion(admission, request.RowVersion);
        await GetSignedInDoctorIdAsync(cancellationToken);
        admission.CancelDischargeOrder();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(admission, cancellationToken);
    }

    /// <summary>
    /// Administrator's correction after the final bill (e.g. the patient's condition changed before leaving): the stay goes
    /// back to Admitted and the discharge order is cancelled. The finalized final invoice stays payable; bed days from here
    /// on are posted through the same charge contract, which Billing routes to a supplementary running bill.
    /// </summary>
    public async Task<AdmissionDto> ReopenAsync(Guid id, ReopenDto request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var admission = await GetAdmissionAsync(id, cancellationToken);
        admissions.SetExpectedRowVersion(admission, request.RowVersion);
        if (admission.Status != AdmissionStatus.FinalBilled)
        {
            throw new BusinessException("ADMISSION_INVALID_STATUS", admission.Status == AdmissionStatus.Discharged
                ? "The patient has already left. Admit the patient again instead."
                : "Only a stay with a prepared final bill can be reopened; cancel the discharge order instead.");
        }

        admission.Reopen(userId, request.Reason, dateTime.UtcNow);
        auditService.Record(new AuditEvent("Reopened", IpdPermissions.Module, nameof(Admission), admission.Id.ToString(),
            new { Status = AdmissionStatus.FinalBilled }, new { Status = AdmissionStatus.Admitted, request.Reason }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var dto = await ToDtoAsync(admission, cancellationToken);
        await notifications.NotifyEmployeeAsync(admission.AttendingDoctorId, new NotificationMessage(NotificationTypes.PatientAdmitted,
            $"Stay reopened · {dto.Summary.WardName} bed {dto.Summary.BedNumber}",
            $"{dto.Summary.PatientName} ({dto.Summary.PatientMRNumber}) stays after the final bill — {admission.ReopenReason}",
            $"/ipd/admissions/{admission.Id}"), cancellationToken);
        return dto;
    }

    /// <summary>
    /// Fixes the discharge time, posts the remaining bed days (noon rule) and finalizes the bill so the cashier can settle it.
    /// Each step is safe to repeat: a retry finds the bill already finalized.
    /// </summary>
    public async Task<AdmissionDto> PrepareFinalBillAsync(Guid id, AdmissionRowVersionDto request, CancellationToken cancellationToken)
    {
        var admission = await GetAdmissionAsync(id, cancellationToken);
        admissions.SetExpectedRowVersion(admission, request.RowVersion);
        if (admission.Status != AdmissionStatus.DischargeOrdered)
        {
            throw new BusinessException("ADMISSION_INVALID_STATUS", admission.Status == AdmissionStatus.Admitted
                ? "The doctor must order the discharge before the final bill."
                : "The final bill has already been prepared.");
        }

        // The discharge time is the doctor's order time, not when the cashier prepares the bill (Q-IPD-1: a patient
        // ordered out before noon is not charged for that day, however long the paperwork takes).
        var dischargedAt = admission.ChargeCutOff(dateTime.UtcNow);
        await chargePoster.PostAsync(admission, dischargedAt, cancellationToken);
        await charges.FinalizeRunningBillAsync(ChargeSources.IpdAdmission, admission.Id, cancellationToken);
        admission.MarkFinalBilled(dischargedAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var dto = await ToDtoAsync(admission, cancellationToken);
        if (dto.Bill is { DueAmount: > 0 } bill)
        {
            await notifications.NotifyPermissionAsync(BillingCollectPermission, new NotificationMessage(NotificationTypes.FinalBillReady,
                $"Final bill ready · {bill.InvoiceNumber}", $"{dto.Summary.PatientName} ({dto.Summary.PatientMRNumber}): {bill.DueAmount:0.00} due before discharge.",
                $"/billing/invoices/{bill.InvoiceId}"), cancellationToken);
        }

        return dto;
    }

    /// <summary>The patient leaves and the bed is freed: the bill must be settled, or an administrator records why not.</summary>
    public async Task<AdmissionDto> ReleaseAsync(Guid id, ReleaseDto request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var admission = await GetAdmissionAsync(id, cancellationToken);
        admissions.SetExpectedRowVersion(admission, request.RowVersion);
        var bill = await charges.GetBillAsync(ChargeSources.IpdAdmission, admission.Id, cancellationToken);
        var due = bill?.DueAmount ?? 0;

        string? overrideReason = null;
        if (due > 0)
        {
            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new BusinessException(IpdErrorCodes.BillNotSettled,
                    $"{due:0.00} is still due on bill {bill!.InvoiceNumber}. Settle it at the cash counter, or an administrator can discharge with due.");
            }

            if (!await permissions.HasPermissionAsync(userId, IpdPermissions.DischargeOverride, cancellationToken))
            {
                throw new ForbiddenException("Only an administrator can discharge a patient whose bill is not fully paid.");
            }

            overrideReason = request.OverrideReason;
        }

        var bed = await beds.GetByIdAsync(admission.CurrentBed?.BedId ?? Guid.Empty, cancellationToken)
            ?? throw new BusinessException(IpdErrorCodes.BedInvalid, "The patient has no current bed.");
        admission.Release(bed, dateTime.UtcNow, due, overrideReason is null ? null : userId, overrideReason);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (admission.DischargeType == DischargeType.Death)
        {
            await MarkDeceasedSafelyAsync(admission, cancellationToken);
        }

        var dto = await ToDtoAsync(admission, cancellationToken);
        await notifications.NotifyEmployeeAsync(admission.AttendingDoctorId, new NotificationMessage(NotificationTypes.PatientDischarged,
            $"Patient discharged · {admission.AdmissionNumber}", $"{dto.Summary.PatientName} ({dto.Summary.PatientMRNumber}) has left the ward.",
            $"/ipd/admissions/{admission.Id}"), cancellationToken);
        return dto;
    }

    // ---------- helpers ----------

    /// <summary>Owned by Billing; holders collect payments (final-bill notification).</summary>
    private const string BillingCollectPermission = "Billing.Payment.Collect";

    /// <summary>
    /// The price-list admission charge (service ADM), posted once at admission when active and priced. Not posted by the
    /// periodic job, so pricing it later never bills stays that began before. A failure is logged; the cashier can add it.
    /// </summary>
    private async Task PostAdmissionFeeSafelyAsync(Admission admission, CancellationToken cancellationToken)
    {
        try
        {
            if (await prices.GetActiveAsync(StandardServiceCodes.Admission, cancellationToken) is { Price: > 0 } fee)
            {
                await charges.AddToRunningBillAsync(new RunningChargeRequest(ChargeSources.IpdAdmission, admission.Id, admission.AdmissionNumber,
                    admission.PatientId, [new RunningChargeLine("ADMISSION", fee.Name, fee.Category, 1, fee.Price)]), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Admission charge for {AdmissionNumber} could not be posted", admission.AdmissionNumber);
        }
    }

    /// <summary>A death discharge records the patient as deceased; a failure is logged and staff can correct the record.</summary>
    private async Task MarkDeceasedSafelyAsync(Admission admission, CancellationToken cancellationToken)
    {
        try
        {
            await patientStatus.MarkDeceasedAsync(admission.PatientId, admission.DischargedAt ?? admission.ReleasedAt ?? dateTime.UtcNow,
                $"IPD discharge {admission.AdmissionNumber}", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Patient of admission {AdmissionNumber} could not be marked deceased", admission.AdmissionNumber);
        }
    }

    /// <summary>Posting failures never undo the clinical action; the periodic job posts the missing days later.</summary>
    private async Task PostChargesSafelyAsync(Admission admission, CancellationToken cancellationToken)
    {
        try
        {
            await chargePoster.PostAsync(admission, null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Bed charges for admission {AdmissionNumber} could not be posted now; the periodic job will retry", admission.AdmissionNumber);
        }
    }

    /// <summary>A bed that can take a patient now: in use, Available (not reserved, occupied or out of service) and in an active ward.</summary>
    private async Task<Bed> GetAssignableBedAsync(Guid bedId, CancellationToken cancellationToken)
    {
        var bed = await beds.GetByIdAsync(bedId, cancellationToken)
            ?? throw new BusinessException(IpdErrorCodes.BedInvalid, "Select a bed.", "bedId");
        if (bed.UnavailableReason is { } reason)
        {
            throw new BusinessException(IpdErrorCodes.BedUnavailable, $"{reason} Choose another bed.", "bedId");
        }

        var ward = await wards.GetByIdAsync(bed.WardId, cancellationToken);
        if (ward is not { IsActive: true })
        {
            throw new BusinessException(IpdErrorCodes.WardInactive, $"Ward {ward?.Name ?? "of this bed"} is closed. Choose a bed in an active ward.", "bedId");
        }

        return bed;
    }

    private async Task<DoctorSummary> GetCurrentDoctorAsync(Guid doctorId, string field, CancellationToken cancellationToken) =>
        await doctorLookup.GetAsync(doctorId, cancellationToken) is { IsCurrent: true } doctor
            ? doctor
            : throw new BusinessException(IpdErrorCodes.DoctorUnavailable, "Select a current doctor.", field);

    /// <summary>Discharge orders are signed by a doctor: the login must be linked to an employee with a doctor profile.</summary>
    private async Task<Guid> GetSignedInDoctorIdAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var employeeId = await userLookup.GetEmployeeIdAsync(userId, cancellationToken);
        return employeeId is { } id && await doctorLookup.GetAsync(id, cancellationToken) is not null
            ? id
            : throw new BusinessException(IpdErrorCodes.NotADoctor,
                "Your login is not linked to a doctor profile. Ask an administrator to link it to your employee record.");
    }

    private async Task<bool> CanViewClinicalAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        return await permissions.HasPermissionAsync(userId, IpdPermissions.ClinicalView, cancellationToken)
               || await permissions.HasPermissionAsync(userId, IpdPermissions.DischargeOrder, cancellationToken);
    }

    private async Task<Admission> GetAdmissionAsync(Guid id, CancellationToken cancellationToken) =>
        await admissions.GetWithBedsAsync(id, cancellationToken) ?? throw new NotFoundException("Admission", id);

    private async Task<string?> DepartmentNameAsync(Guid? id, CancellationToken cancellationToken) =>
        id is { } value ? (await departmentLookup.GetAsync(value, cancellationToken))?.Name : null;

    private async Task<DischargeInfoDto?> DischargeInfoAsync(Admission a, CancellationToken cancellationToken)
    {
        if (a.DischargeType is not { } type)
        {
            return null;
        }

        var orderedBy = a.DischargeOrderedBy is { } doctorId ? (await doctorLookup.GetAsync(doctorId, cancellationToken))?.FullName : null;
        return new DischargeInfoDto(type, a.FinalDiagnosis, a.HospitalCourse, a.DischargeMedications, a.DischargeAdvice, a.FollowUpDate, orderedBy,
            a.DischargeOrderedAt);
    }

    private sealed record ListContext(
        IReadOnlyDictionary<Guid, PatientSummary> Patients,
        IReadOnlyDictionary<Guid, DoctorSummary> Doctors,
        IReadOnlyDictionary<Guid, Bed> Beds,
        IReadOnlyDictionary<Guid, Ward> Wards,
        IReadOnlyDictionary<Guid, BedCategory> Categories);

    private async Task<ListContext> LoadContextAsync(IEnumerable<Admission> list, CancellationToken cancellationToken)
    {
        var items = list.ToList();
        var patients = await patientLookup.GetManyAsync(items.Select(a => a.PatientId).Distinct().ToList(), cancellationToken);
        var doctors = await doctorLookup.GetManyAsync(items.Select(a => a.AttendingDoctorId).Distinct().ToList(), cancellationToken);
        var bedList = await beds.GetByIdsAsync(items.SelectMany(a => a.Beds.Select(b => b.BedId)).Distinct().ToList(), cancellationToken);
        var wardList = await wards.GetByIdsAsync(bedList.Select(b => b.WardId).Distinct().ToList(), cancellationToken);
        var categoryList = await categories.GetByIdsAsync(bedList.Select(b => b.CategoryId).Distinct().ToList(), cancellationToken);
        return new ListContext(patients.ToDictionary(p => p.Id), doctors.ToDictionary(d => d.DoctorId), bedList.ToDictionary(b => b.Id),
            wardList.ToDictionary(w => w.Id), categoryList.ToDictionary(c => c.Id));
    }

    private AdmissionSummaryDto ToSummary(Admission a, ListContext context)
    {
        var patient = context.Patients.GetValueOrDefault(a.PatientId);
        var stay = a.CurrentBed ?? a.Beds.OrderBy(b => b.StartedAt).LastOrDefault();
        var bed = stay is null ? null : context.Beds.GetValueOrDefault(stay.BedId);
        var days = BedDays.For(a.AdmittedAt, a.DischargedAt ?? a.ChargeCutOff(dateTime.UtcNow), dateTime).Count;
        return new AdmissionSummaryDto(
            a.Id, a.AdmissionNumber, a.PatientId, patient?.MRNumber, patient?.FullName, patient?.Gender, patient?.DateOfBirth, a.AttendingDoctorId,
            context.Doctors.GetValueOrDefault(a.AttendingDoctorId)?.FullName, bed is null ? null : context.Wards.GetValueOrDefault(bed.WardId)?.Name,
            bed?.BedNumber, a.AdmittedAt, a.DischargedAt, days, a.Status);
    }

    private async Task<AdmissionDto> ToDtoAsync(Admission a, CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync([a], cancellationToken);
        var currentBed = a.CurrentBed is { } stay ? context.Beds.GetValueOrDefault(stay.BedId) : null;
        var category = currentBed is null ? null : context.Categories.GetValueOrDefault(currentBed.CategoryId);
        var bill = await charges.GetBillAsync(ChargeSources.IpdAdmission, a.Id, cancellationToken);
        var balance = await balances.GetAsync(a.PatientId, cancellationToken);
        var history = a.Beds.OrderBy(b => b.StartedAt).Select(b =>
        {
            var bed = context.Beds.GetValueOrDefault(b.BedId);
            return new BedStayDto(b.BedId, bed is null ? null : context.Wards.GetValueOrDefault(bed.WardId)?.Name, bed?.BedNumber, bed?.Room,
                bed is null ? null : context.Categories.GetValueOrDefault(bed.CategoryId)?.Name, b.StartedAt, b.EndedAt, b.Reason);
        }).ToList();

        return new AdmissionDto(
            ToSummary(a, context),
            await DepartmentNameAsync(a.DepartmentId, cancellationToken),
            a.Source,
            a.SourceReference,
            a.Reason,
            a.EmergencyContactName,
            a.EmergencyContactRelation,
            a.EmergencyContactPhone,
            currentBed?.Id,
            category?.Name,
            category?.DailyRate ?? 0,
            category?.SuggestedDeposit ?? 0,
            history,
            a.DischargeOrderedAt is not null,
            await CanViewClinicalAsync(cancellationToken) ? await DischargeInfoAsync(a, cancellationToken) : null,
            a.ReleasedAt,
            a.ReleasedWithDue,
            a.ReleaseOverrideReason,
            a.ReopenedAt,
            a.ReopenReason,
            bill is null ? null : new BillInfoDto(bill.InvoiceId, bill.InvoiceNumber, bill.Status, bill.Subtotal, bill.NetAmount, bill.PaidAmount, bill.DueAmount),
            balance.AdvanceBalance,
            a.RowVersion);
    }
}
