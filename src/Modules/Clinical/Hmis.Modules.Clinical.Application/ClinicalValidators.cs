using FluentValidation;

namespace Hmis.Modules.Clinical.Application;

/// <summary>Plausible human ranges; values outside these are almost always typing mistakes.</summary>
public sealed class VitalSignRequestValidator : AbstractValidator<VitalSignRequestDto>
{
    public VitalSignRequestValidator()
    {
        RuleFor(x => x.Systolic).InclusiveBetween(40, 300);
        RuleFor(x => x.Diastolic).InclusiveBetween(20, 200);
        RuleFor(x => x.Diastolic).LessThan(x => x.Systolic).When(x => x.Systolic.HasValue && x.Diastolic.HasValue)
            .WithMessage("Diastolic pressure must be lower than systolic.");
        RuleFor(x => x.Pulse).InclusiveBetween(20, 250);
        RuleFor(x => x.TemperatureF).InclusiveBetween(85m, 115m).WithMessage("Enter the temperature in °F (85–115).");
        RuleFor(x => x.RespiratoryRate).InclusiveBetween(4, 80);
        RuleFor(x => x.SpO2).InclusiveBetween(50, 100);
        RuleFor(x => x.WeightKg).InclusiveBetween(0.3m, 400m);
        RuleFor(x => x.HeightCm).InclusiveBetween(30m, 250m);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class EnteredInErrorValidator : AbstractValidator<EnteredInErrorDto>
{
    public EnteredInErrorValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class ConsultationSaveValidator : AbstractValidator<ConsultationSaveDto>
{
    public ConsultationSaveValidator()
    {
        RuleFor(x => x.ChiefComplaint).MaximumLength(1000);
        RuleFor(x => x.HistoryOfPresentIllness).MaximumLength(4000);
        RuleFor(x => x.Examination).MaximumLength(4000);
        RuleFor(x => x.Advice).MaximumLength(2000);
        RuleFor(x => x.FollowUpInstructions).MaximumLength(500);
        RuleFor(x => x.ReferralTo).MaximumLength(200);
        RuleFor(x => x.ReferralReason).MaximumLength(500);

        RuleFor(x => x.Diagnoses).Must(d => d.Count <= 20).WithMessage("At most 20 diagnoses.");
        RuleForEach(x => x.Diagnoses).ChildRules(d =>
        {
            d.RuleFor(x => x.Type).IsInEnum();
            d.RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        });

        RuleFor(x => x.Prescription).Must(p => p.Count <= 40).WithMessage("At most 40 medicines.");
        RuleForEach(x => x.Prescription).ChildRules(p =>
        {
            p.RuleFor(x => x.MedicineName).NotEmpty().MaximumLength(150);
            p.RuleFor(x => x.Form).MaximumLength(30);
            p.RuleFor(x => x.Strength).MaximumLength(50);
            p.RuleFor(x => x.Route).MaximumLength(30);
            p.RuleFor(x => x.DosePattern).NotEmpty().MaximumLength(30);
            p.RuleFor(x => x.Timing).IsInEnum();
            p.RuleFor(x => x.DurationUnit).IsInEnum();
            p.RuleFor(x => x.DurationValue).InclusiveBetween(1, 365);
            p.RuleFor(x => x.Quantity).InclusiveBetween(1, 10000);
            p.RuleFor(x => x.Instructions).MaximumLength(300);
        });

        RuleFor(x => x.Investigations).Must(i => i.Count <= 40).WithMessage("At most 40 investigations.");
        RuleForEach(x => x.Investigations).ChildRules(i =>
        {
            i.RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            i.RuleFor(x => x.Notes).MaximumLength(300);
        });
    }
}

public sealed class AddendumCreateValidator : AbstractValidator<AddendumCreateDto>
{
    public AddendumCreateValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
    }
}
