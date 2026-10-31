using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Files;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Patient.Domain.Entities;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Application;

public sealed class PatientManager(
    IPatientRepository repository,
    IPatientUnitOfWork unitOfWork,
    INumberSequenceService numberSequences,
    IFileStorageService fileStorage) : IPatientManager, IPatientLookup
{
    public const string MrnSequenceKey = "PATIENT_MRN";
    public const long MaxPhotoBytes = 2 * 1024 * 1024;

    public async Task<PagedResponse<PatientListItemDto>> SearchAsync(PatientSearchQuery query, CancellationToken cancellationToken) =>
        (await repository.SearchAsync(query, cancellationToken)).Map(p => new PatientListItemDto(
            p.Id, p.MRNumber, p.FullName, p.Gender, p.DateOfBirth, p.IsDateOfBirthEstimated, p.MobileNumber,
            p.PresentAddress.District, p.Status));

    public async Task<IReadOnlyList<DuplicateCandidateDto>> FindDuplicatesAsync(DuplicateCheckQuery query, CancellationToken cancellationToken)
    {
        var rows = await repository.FindDuplicateCandidatesAsync(query with
        {
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
        return await ToDtoAsync(patient, cancellationToken);
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

    // ---------- IPatientLookup (contract for other modules) ----------

    async Task<PatientSummary?> IPatientLookup.GetAsync(Guid patientId, CancellationToken cancellationToken) =>
        ToSummary(await repository.GetByIdAsync(patientId, cancellationToken));

    async Task<PatientSummary?> IPatientLookup.GetByMRNumberAsync(string mrNumber, CancellationToken cancellationToken) =>
        ToSummary(await repository.GetByMRNumberAsync(mrNumber.Trim().ToUpperInvariant(), cancellationToken));

    private static PatientSummary? ToSummary(PatientEntity? p) =>
        p is null
            ? null
            : new PatientSummary(p.Id, p.MRNumber, p.FullName, p.Gender.ToString(), p.DateOfBirth, p.MobileNumber,
                p.Status.ToString(), p.MergedIntoPatientId);

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
            d.EmergencyContacts.Select(c => new EmergencyContact(c.Name.Trim(), c.Relation.Trim(), c.MobileNumber.Trim())).ToList());

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

        return new PatientDto(
            p.Id, p.MRNumber, p.FullName, p.FullNameBangla, p.Gender, p.DateOfBirth, p.IsDateOfBirthEstimated, p.BloodGroup,
            p.MobileNumber, p.Email, FromAddress(p.PresentAddress), FromAddress(p.PermanentAddress), p.IdentificationType,
            p.IdentificationNumber, p.GuardianName, p.GuardianRelation,
            p.EmergencyContacts.Select(c => new EmergencyContactDto { Name = c.Name, Relation = c.Relation, MobileNumber = c.MobileNumber }).ToList(),
            p.PhotoPath is not null, p.Status, p.MergedIntoPatientId, mergedIntoMrn, p.CreatedAt, p.RowVersion);
    }
}
