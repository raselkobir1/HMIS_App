using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Patient.Domain.Entities;

public enum Gender
{
    Male,
    Female,
    Other
}

public enum BloodGroup
{
    APositive,
    ANegative,
    BPositive,
    BNegative,
    ABPositive,
    ABNegative,
    OPositive,
    ONegative
}

public enum IdentificationType
{
    NationalId,
    BirthRegistration,
    Passport,
    Other
}

public enum PatientStatus
{
    Active,
    Inactive,

    /// <summary>Duplicate record merged into another patient; read-only and points to the surviving record.</summary>
    Merged
}

/// <summary>Address stored inline on the patient (owned value).</summary>
public sealed record Address(string? Line, string? Upazila, string? District, string? PostCode)
{
    public static Address Empty => new(null, null, null, null);
}

public sealed record EmergencyContact(string Name, string Relation, string MobileNumber);

/// <summary>
/// The permanent patient record (BRD Module 02). One MR number per patient for life; visits, admissions and orders
/// in other modules reference this record instead of creating new ones.
/// </summary>
[Audited]
public sealed class Patient : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    public const int MaxEmergencyContacts = 3;

    private Patient() { }

    /// <summary>Medical Record number, generated from PATIENT_MRN; never changes.</summary>
    public string MRNumber { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;
    public string? FullNameBangla { get; private set; }
    public Gender Gender { get; private set; }
    public DateOnly DateOfBirth { get; private set; }

    /// <summary>True when only the age was known and the date of birth was derived from it.</summary>
    public bool IsDateOfBirthEstimated { get; private set; }

    public BloodGroup? BloodGroup { get; private set; }

    /// <summary>May be a guardian's number and shared by several patients (children, elderly).</summary>
    public string MobileNumber { get; private set; } = string.Empty;

    public string? Email { get; private set; }
    public Address PresentAddress { get; private set; } = Address.Empty;
    public Address PermanentAddress { get; private set; } = Address.Empty;
    public IdentificationType? IdentificationType { get; private set; }
    public string? IdentificationNumber { get; private set; }
    public string? GuardianName { get; private set; }
    public string? GuardianRelation { get; private set; }

    /// <summary>Stored as JSON; at most three contacts.</summary>
    public List<EmergencyContact> EmergencyContacts { get; private set; } = [];

    [NotAudited]
    public string? PhotoPath { get; private set; }

    [NotAudited]
    public string? PhotoContentType { get; private set; }

    public PatientStatus Status { get; private set; } = PatientStatus.Active;
    public Guid? MergedIntoPatientId { get; private set; }
    public uint RowVersion { get; private set; }

    public static Patient Register(string mrNumber, PatientDetails details)
    {
        var patient = new Patient { MRNumber = mrNumber };
        patient.Apply(details);
        return patient;
    }

    public void UpdateDetails(PatientDetails details)
    {
        EnsureEditable();
        Apply(details);
    }

    public void SetPhoto(string? path, string? contentType)
    {
        EnsureEditable();
        PhotoPath = path;
        PhotoContentType = contentType;
    }

    public void Activate()
    {
        EnsureNotMerged();
        Status = PatientStatus.Active;
    }

    public void Deactivate()
    {
        EnsureNotMerged();
        Status = PatientStatus.Inactive;
    }

    /// <summary>Marks this record as a duplicate of <paramref name="survivorId"/>. Nothing is deleted.</summary>
    public void MarkMergedInto(Guid survivorId)
    {
        EnsureNotMerged();
        if (survivorId == Id)
        {
            throw new DomainException("PATIENT_MERGE_SELF", "A patient cannot be merged into itself.");
        }

        Status = PatientStatus.Merged;
        MergedIntoPatientId = survivorId;
    }

    public void EnsureEditable()
    {
        EnsureNotMerged();
        if (Status == PatientStatus.Inactive)
        {
            throw new DomainException("PATIENT_INACTIVE", "This patient record is inactive. Reactivate it before making changes.");
        }
    }

    private void EnsureNotMerged()
    {
        if (Status == PatientStatus.Merged)
        {
            throw new DomainException("PATIENT_MERGED", "This record was merged into another patient and is read-only.");
        }
    }

    private void Apply(PatientDetails d)
    {
        if (d.EmergencyContacts.Count > MaxEmergencyContacts)
        {
            throw new DomainException("PATIENT_TOO_MANY_CONTACTS", $"At most {MaxEmergencyContacts} emergency contacts are allowed.");
        }

        if ((d.IdentificationType is null) != string.IsNullOrWhiteSpace(d.IdentificationNumber))
        {
            throw new DomainException("PATIENT_IDENTIFICATION_INCOMPLETE", "Provide both the identification type and number, or neither.");
        }

        FullName = d.FullName.Trim();
        FullNameBangla = Clean(d.FullNameBangla);
        Gender = d.Gender;
        DateOfBirth = d.DateOfBirth;
        IsDateOfBirthEstimated = d.IsDateOfBirthEstimated;
        BloodGroup = d.BloodGroup;
        MobileNumber = d.MobileNumber.Trim();
        Email = Clean(d.Email);
        PresentAddress = d.PresentAddress;
        PermanentAddress = d.PermanentAddress;
        IdentificationType = d.IdentificationType;
        IdentificationNumber = NormalizeIdentification(d.IdentificationNumber);
        GuardianName = Clean(d.GuardianName);
        GuardianRelation = Clean(d.GuardianRelation);
        EmergencyContacts = d.EmergencyContacts.ToList();
    }

    /// <summary>ID numbers are compared without spaces or dashes and case-insensitively.</summary>
    public static string? NormalizeIdentification(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Editable demographic details, shared by registration and update.</summary>
public sealed record PatientDetails(
    string FullName,
    string? FullNameBangla,
    Gender Gender,
    DateOnly DateOfBirth,
    bool IsDateOfBirthEstimated,
    BloodGroup? BloodGroup,
    string MobileNumber,
    string? Email,
    Address PresentAddress,
    Address PermanentAddress,
    IdentificationType? IdentificationType,
    string? IdentificationNumber,
    string? GuardianName,
    string? GuardianRelation,
    IReadOnlyList<EmergencyContact> EmergencyContacts);
