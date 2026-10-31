using FluentValidation;
using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Validators;
using Hmis.Modules.Patient.Domain;
using Hmis.Modules.Patient.Domain.Entities;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Application;

/// <summary>Rules for demographics (Q-PAT-2: name, gender, date of birth or age, mobile are mandatory).</summary>
public class PatientDetailsValidator<T> : AbstractValidator<T> where T : PatientDetailsDto
{
    public PatientDetailsValidator(IDateTimeProvider dateTime)
    {
        var today = dateTime.LocalToday;

        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FullNameBangla).MaximumLength(150);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .LessThanOrEqualTo(today).WithMessage("Date of birth cannot be in the future.")
            .GreaterThan(today.AddYears(-130)).WithMessage("Date of birth is not plausible.");
        RuleFor(x => x.BloodGroup).IsInEnum();
        RuleFor(x => x.MobileNumber).NotEmpty().Matches(@"^\+?[0-9\s-]{6,20}$").WithMessage("Mobile number is not valid.");
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PresentAddress).NotNull().SetValidator(new AddressValidator());
        RuleFor(x => x.PermanentAddress).NotNull().SetValidator(new AddressValidator());
        RuleFor(x => x.IdentificationType).IsInEnum();
        RuleFor(x => x.IdentificationNumber)
            .MaximumLength(30)
            .Matches(@"^[A-Za-z0-9\s-]+$").WithMessage("Identification number may contain only letters, digits, spaces and '-'.")
            .When(x => !string.IsNullOrWhiteSpace(x.IdentificationNumber));
        RuleFor(x => x.IdentificationNumber)
            .NotEmpty().When(x => x.IdentificationType.HasValue)
            .WithMessage("Enter the identification number for the selected type.");
        RuleFor(x => x.IdentificationType)
            .NotNull().When(x => !string.IsNullOrWhiteSpace(x.IdentificationNumber))
            .WithMessage("Select the identification type.");
        RuleFor(x => x.GuardianName).MaximumLength(150);
        RuleFor(x => x.GuardianRelation).MaximumLength(50);
        RuleFor(x => x.EmergencyContacts)
            .NotNull()
            .Must(c => c.Count <= PatientEntity.MaxEmergencyContacts)
            .WithMessage($"At most {PatientEntity.MaxEmergencyContacts} emergency contacts are allowed.");
        RuleForEach(x => x.EmergencyContacts).SetValidator(new EmergencyContactValidator());
    }
}

public sealed class PatientRegisterValidator(IDateTimeProvider dateTime) : PatientDetailsValidator<PatientRegisterDto>(dateTime);

public sealed class PatientUpdateValidator(IDateTimeProvider dateTime) : PatientDetailsValidator<PatientUpdateDto>(dateTime);

public sealed class AddressValidator : AbstractValidator<AddressDto>
{
    public AddressValidator()
    {
        RuleFor(x => x.Line).MaximumLength(250);
        RuleFor(x => x.Upazila).MaximumLength(100);
        RuleFor(x => x.District).Must(BangladeshDistricts.IsValid).WithMessage("Select a district from the list.");
        RuleFor(x => x.PostCode).Matches(@"^\d{4}$").When(x => !string.IsNullOrWhiteSpace(x.PostCode))
            .WithMessage("Post code must be 4 digits.");
    }
}

public sealed class EmergencyContactValidator : AbstractValidator<EmergencyContactDto>
{
    public EmergencyContactValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Relation).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MobileNumber).NotEmpty().Matches(@"^\+?[0-9\s-]{6,20}$").WithMessage("Mobile number is not valid.");
    }
}

public sealed class PatientSearchQueryValidator : PaginationRequestValidator<PatientSearchQuery>
{
    public PatientSearchQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.SortBy).Empty().WithMessage("Patient search is ordered by relevance (MR number, then name).");
    }
}

public sealed class DuplicateCheckQueryValidator : AbstractValidator<DuplicateCheckQuery>
{
    public DuplicateCheckQueryValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(150);
        RuleFor(x => x.MobileNumber).MaximumLength(20);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.IdentificationType).IsInEnum();
        RuleFor(x => x.IdentificationNumber).MaximumLength(30);
    }
}

public sealed class AllergyRequestValidator : AbstractValidator<AllergyRequestDto>
{
    public AllergyRequestValidator()
    {
        RuleFor(x => x.Allergen).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.Reaction).MaximumLength(500);
    }
}

public sealed class AllergyStatusValidator : AbstractValidator<AllergyStatusDto>
{
    public AllergyStatusValidator() => RuleFor(x => x.Status).IsInEnum();
}

public sealed class MedicalHistoryRequestValidator : AbstractValidator<MedicalHistoryRequestDto>
{
    public MedicalHistoryRequestValidator(IDateTimeProvider dateTime)
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.OnsetDate).LessThanOrEqualTo(dateTime.LocalToday).When(x => x.OnsetDate.HasValue)
            .WithMessage("Onset date cannot be in the future.");
    }
}

public sealed class MergeRequestCreateValidator : AbstractValidator<MergeRequestCreateDto>
{
    public MergeRequestCreateValidator()
    {
        RuleFor(x => x.SourcePatientId).NotEmpty();
        RuleFor(x => x.TargetPatientId).NotEmpty().NotEqual(x => x.SourcePatientId).WithMessage("Choose two different patients.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class MergeReviewValidator : AbstractValidator<MergeReviewDto>
{
    public MergeReviewValidator() => RuleFor(x => x.Note).MaximumLength(500);
}

public sealed class MergeRequestQueryValidator : PaginationRequestValidator<MergeRequestQuery>
{
    public MergeRequestQueryValidator() => RuleFor(x => x.Status).IsInEnum();
}
