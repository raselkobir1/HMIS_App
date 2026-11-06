using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Files;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Patient.Domain.Entities;
using Microsoft.Extensions.Logging;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Application;

public sealed class PatientManager(
    IPatientRepository repository,
    IPatientUnitOfWork unitOfWork,
    INumberSequenceService numberSequences,
    IFileStorageService fileStorage,
    IServicePriceLookup prices,
    IChargeService charges,
    IDateTimeProvider dateTime,
    ILogger<PatientManager> logger) : IPatientManager
{
    public const string MrnSequenceKey = "PATIENT_MRN";
    public const long MaxPhotoBytes = 2 * 1024 * 1024;

    public async Task<PagedResponse<PatientListItemDto>> SearchAsync(PatientSearchQuery query, CancellationToken cancellationToken) =>
        (await repository.SearchAsync(query, cancellationToken)).Map(p => new PatientListItemDto(
            p.Id, p.MRNumber, p.FullName, p.Gender, p.DateOfBirth, p.IsDateOfBirthEstimated, p.MobileNumber,
            p.PresentAddress.District, p.Status));

    public async Task<PatientCountsDto> GetCountsAsync(CancellationToken cancellationToken)
    {
        var (today, active) = await repository.CountAsync(dateTime.StartOfLocalDayUtc(dateTime.LocalToday), cancellationToken);
        return new PatientCountsDto(today, active);
    }

    public async Task<IReadOnlyList<DuplicateCandidateDto>> FindDuplicatesAsync(DuplicateCheckQuery query, CancellationToken cancellationToken)
    {
        // Stored numbers are digits only, so the typed value is reduced the same way before comparing.
        var mobileDigits = new string((query.MobileNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var rows = await repository.FindDuplicateCandidatesAsync(query with
        {
            MobileNumber = mobileDigits.Length >= PatientEntity.MinMobileDigits ? mobileDigits : null,
            IdentificationNumber = PatientEntity.NormalizeIdentification(query.IdentificationNumber)
        }, cancellationToken);

        return rows.Select(r =>
        {
            var reasons = new List<string>();
            if (r.SameIdentification) reasons.Add("Same identification number");
            if (r.SameMobileSimilarName) reasons.Add("Same mobile number and similar name");
            if (r.SameBirthSimilarName) reasons.Add("Similar name, same date of birth and gender");
            return new DuplicateCandidateDto(r.Id, r.MRNumber, r.FullName, r.Gender, r.DateOfBirth, r.MobileNumber, reasons, r.SameIdentification);
        }).ToList();
    }

    public async Task<PatientDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDtoAsync(await GetAsync(id, cancellationToken), cancellationToken);

    public async Task<PatientDto> GetByMRNumberAsync(string mrNumber, CancellationToken cancellationToken) =>
        await ToDtoAsync(
            await repository.GetByMRNumberAsync(mrNumber.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("Patient", mrNumber),
            cancellationToken);

    public async Task<PatientDto> RegisterAsync(PatientRegisterDto request, CancellationToken cancellationToken)
    {
        var details = ToDetails(request);
        await EnsureIdentificationUniqueAsync(details, null, cancellationToken);

        // MR numbers come from Administration's atomic sequence; a failed save leaves a gap, never a duplicate.
        var mrNumber = await numberSequences.NextAsync(MrnSequenceKey, cancellationToken);
        var patient = PatientEntity.Register(mrNumber, details);

        await repository.AddAsync(patient, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(patient, cancellationToken) with { RegistrationBill = await BillRegistrationAsync(patient, cancellationToken) };
    }

    public async Task<PatientDto> UpdateAsync(Guid id, PatientUpdateDto request, CancellationToken cancellationToken)
    {
        var patient = await GetAsync(id, cancellationToken);
        repository.SetExpectedRowVersion(patient, request.RowVersion);

        var details = ToDetails(request);
        await EnsureIdentificationUniqueAsync(details, id, cancellationToken);

        patient.UpdateDetails(details);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(patient, cancellationToken);
    }

    public async Task<PatientDto> SetStatusAsync(Guid id, PatientStatusUpdateDto request, CancellationToken cancellationToken)
    {
        var patient = await GetAsync(id, cancellationToken);
        repository.SetExpectedRowVersion(patient, request.RowVersion);
        if (request.IsActive)
        {
            patient.Activate();
        }
        else
        {
            patient.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(patient, cancellationToken);
    }

    public async Task<PatientDto> UploadPhotoAsync(Guid id, Stream content, long length, CancellationToken cancellationToken)
    {
        if (length <= 0 || length > MaxPhotoBytes)
        {
            throw new BusinessException(PatientErrorCodes.PhotoInvalid, "The photo must be an image of at most 2 MB.", "file");
        }

        var patient = await GetAsync(id, cancellationToken);
        patient.EnsureEditable();

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var type = FileSignatures.DetectImage(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
            ?? throw new BusinessException(PatientErrorCodes.PhotoInvalid, "The photo must be a PNG, JPEG or WebP image.", "file");

        var previous = patient.PhotoPath;
        buffer.Position = 0;
        var stored = await fileStorage.SaveAsync(buffer, $"patients/{patient.Id:N}/photo", type.Extension, type.ContentType, cancellationToken);
        patient.SetPhoto(stored.StoragePath, stored.ContentType);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(stored.StoragePath, CancellationToken.None);
            throw;
        }

        if (previous is not null)
        {
            await fileStorage.DeleteAsync(previous, cancellationToken);
        }

        return await ToDtoAsync(patient, cancellationToken);
    }

    public async Task<PatientDto> RemovePhotoAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await GetAsync(id, cancellationToken);
        var previous = patient.PhotoPath;
        patient.SetPhoto(null, null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (previous is not null)
        {
            await fileStorage.DeleteAsync(previous, cancellationToken);
        }

        return await ToDtoAsync(patient, cancellationToken);
    }

    public async Task<DocumentContent?> OpenPhotoAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await GetAsync(id, cancellationToken);
        if (patient.PhotoPath is null || patient.PhotoContentType is null)
        {
            return null;
        }

        var stream = await fileStorage.OpenReadAsync(patient.PhotoPath, cancellationToken);
        return stream is null ? null : new DocumentContent(stream, patient.PhotoContentType, $"{patient.MRNumber}-photo");
    }

    /// <summary>
    /// Bills the price-list registration fee (service REG) once per patient when it is active and priced. Billing never blocks
    /// registration: a failure is logged and the cashier can add the fee by hand.
    /// </summary>
    private async Task<RegistrationBillDto?> BillRegistrationAsync(PatientEntity patient, CancellationToken cancellationToken)
    {
        try
        {
            if (await prices.GetActiveAsync(StandardServiceCodes.Registration, cancellationToken) is not { Price: > 0 } fee)
            {
                return null;
            }

            var bill = await charges.PostAsync(new ChargeRequest(ChargeSources.PatientRegistration, patient.Id, patient.MRNumber, patient.Id,
                [new ChargeLine(fee.Name, fee.Category, 1, fee.Price)]), cancellationToken);
            return new RegistrationBillDto(bill.InvoiceId, bill.InvoiceNumber, bill.NetAmount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Registration fee for {MRNumber} could not be billed", patient.MRNumber);
            return null;
        }
    }

    // ---------- helpers ----------

    private async Task<PatientEntity> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Patient", id);

    /// <summary>An identical ID document number is the only hard duplicate block (Q-PAT-4); other matches are warnings.</summary>
    private async Task EnsureIdentificationUniqueAsync(PatientDetails details, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (details.IdentificationType is { } type && PatientEntity.NormalizeIdentification(details.IdentificationNumber) is { } number &&
            await repository.IdentificationExistsAsync(type, number, excludeId, cancellationToken))
        {
            throw new ConflictException(
                "Another patient is already registered with this identification number. Open that record instead of creating a duplicate.",
                PatientErrorCodes.IdentificationDuplicate);
        }
    }

    private static PatientDetails ToDetails(PatientDetailsDto d) =>
        new(d.FullName, d.FullNameBangla, d.Gender, d.DateOfBirth, d.IsDateOfBirthEstimated, d.BloodGroup, d.MobileNumber, d.Email,
            ToAddress(d.PresentAddress), ToAddress(d.PermanentAddress), d.IdentificationType, d.IdentificationNumber,
            d.GuardianName, d.GuardianRelation,
            d.EmergencyContacts.Select(c => new EmergencyContact(c.Name, c.Relation, c.MobileNumber)).ToList());

    private static Address ToAddress(AddressDto a)
    {
        static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        return new Address(Clean(a.Line), Clean(a.Upazila), Clean(a.District), Clean(a.PostCode));
    }

    private static AddressDto FromAddress(Address a) =>
        new() { Line = a.Line, Upazila = a.Upazila, District = a.District, PostCode = a.PostCode };

    private async Task<PatientDto> ToDtoAsync(PatientEntity p, CancellationToken cancellationToken)
    {
        string? mergedIntoMrn = null;
        if (p.MergedIntoPatientId is { } survivorId)
        {
            mergedIntoMrn = (await repository.GetByIdAsync(survivorId, cancellationToken))?.MRNumber;
        }

        var merged = await PatientContractServices.CollectMergedAsync(repository, p.Id, cancellationToken);

        return new PatientDto(
            p.Id, p.MRNumber, p.FullName, p.FullNameBangla, p.Gender, p.DateOfBirth, p.IsDateOfBirthEstimated, p.BloodGroup,
            p.MobileNumber, p.Email, FromAddress(p.PresentAddress), FromAddress(p.PermanentAddress), p.IdentificationType,
            p.IdentificationNumber, p.GuardianName, p.GuardianRelation,
            p.EmergencyContacts.Select(c => new EmergencyContactDto { Name = c.Name, Relation = c.Relation, MobileNumber = c.MobileNumber }).ToList(),
            p.PhotoPath is not null, p.Status, p.MergedIntoPatientId, mergedIntoMrn, p.DeceasedAt,
            merged.Select(m => new MergedRecordDto(m.Id, m.MRNumber)).ToList(), p.CreatedAt, p.RowVersion);
    }
}
