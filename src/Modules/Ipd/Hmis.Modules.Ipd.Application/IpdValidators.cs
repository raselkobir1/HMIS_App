using FluentValidation;
using Hmis.BuildingBlocks.Application.Validators;

namespace Hmis.Modules.Ipd.Application;

public sealed class BedCategorySaveValidator : AbstractValidator<BedCategorySaveDto>
{
    public BedCategorySaveValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DailyRate).InclusiveBetween(0m, 1_000_000m).PrecisionScale(12, 2, true);
        RuleFor(x => x.SuggestedDeposit).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
    }
}

public sealed class WardSaveValidator : AbstractValidator<WardSaveDto>
{
    public WardSaveValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("Use letters, digits and hyphens only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Building).MaximumLength(100);
        RuleFor(x => x.Floor).MaximumLength(30);
    }
}

public sealed class BedSaveValidator : AbstractValidator<BedSaveDto>
{
    public BedSaveValidator()
    {
        RuleFor(x => x.WardId).NotEmpty();
        RuleFor(x => x.BedNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Room).MaximumLength(30);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

public sealed class BedStatusValidator : AbstractValidator<BedStatusDto>
{
    public BedStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

public sealed class AdmitValidator : AbstractValidator<AdmitDto>
{
    public AdmitValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.AttendingDoctorId).NotEmpty();
        RuleFor(x => x.BedId).NotEmpty().WithMessage("Select a bed.");
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.SourceReference).MaximumLength(50);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).MaximumLength(100);
        RuleFor(x => x.EmergencyContactRelation).MaximumLength(50);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(20).Matches(@"^\+?[0-9 -]{6,20}$").When(x => !string.IsNullOrWhiteSpace(x.EmergencyContactPhone))
            .WithMessage("Enter a valid phone number.");
    }
}

public sealed class TransferValidator : AbstractValidator<TransferDto>
{
    public TransferValidator()
    {
        RuleFor(x => x.BedId).NotEmpty().WithMessage("Select a bed.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}

public sealed class ChangeDoctorValidator : AbstractValidator<ChangeDoctorDto>
{
    public ChangeDoctorValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
    }
}

public sealed class DischargeOrderValidator : AbstractValidator<DischargeOrderDto>
{
    public DischargeOrderValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.FinalDiagnosis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.HospitalCourse).MaximumLength(4000);
        RuleFor(x => x.DischargeMedications).MaximumLength(2000);
        RuleFor(x => x.Advice).MaximumLength(2000);
    }
}

public sealed class ReleaseValidator : AbstractValidator<ReleaseDto>
{
    public ReleaseValidator()
    {
        RuleFor(x => x.OverrideReason).MaximumLength(250);
    }
}

public sealed class ReopenValidator : AbstractValidator<ReopenDto>
{
    public ReopenValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class AdmissionListQueryValidator : PaginationRequestValidator<AdmissionListQuery>
{
    public AdmissionListQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.SortBy).Empty().WithMessage("Admissions are sorted by admission time, newest first.");
    }
}
