using FluentValidation;

namespace Hmis.Modules.Nursing.Application;

public sealed class EnteredInErrorValidator : AbstractValidator<EnteredInErrorDto>
{
    public EnteredInErrorValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class AdministrationCreateValidator : AbstractValidator<AdministrationCreateDto>
{
    public AdministrationCreateValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.DoseGiven).MaximumLength(50);
        RuleFor(x => x.Reason).MaximumLength(250);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class IntakeOutputCreateValidator : AbstractValidator<IntakeOutputCreateDto>
{
    public IntakeOutputCreateValidator()
    {
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(30);
        RuleFor(x => x.VolumeMl).InclusiveBetween(1, 5000).WithMessage("Enter a volume between 1 and 5000 ml.");
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class NursingNoteCreateValidator : AbstractValidator<NursingNoteCreateDto>
{
    public NursingNoteCreateValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public sealed class HandoverSaveValidator : AbstractValidator<HandoverSaveDto>
{
    public HandoverSaveValidator()
    {
        RuleFor(x => x.WardId).NotEmpty();
        RuleFor(x => x.Shift).IsInEnum();
        RuleFor(x => x.Summary).MaximumLength(4000);
        RuleFor(x => x.Items).Must(i => i.Count <= 100).WithMessage("At most 100 patient notes.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.AdmissionId).NotEmpty();
            i.RuleFor(x => x.Note).MaximumLength(1000);
        });
    }
}

public sealed class RosterCreateValidator : AbstractValidator<RosterCreateDto>
{
    public RosterCreateValidator()
    {
        RuleFor(x => x.WardId).NotEmpty();
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Shift).IsInEnum();
    }
}
