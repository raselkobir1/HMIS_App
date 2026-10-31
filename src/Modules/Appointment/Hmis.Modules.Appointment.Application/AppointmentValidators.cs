using FluentValidation;
using Hmis.BuildingBlocks.Application.Validators;
using Hmis.Modules.Appointment.Domain.Entities;

namespace Hmis.Modules.Appointment.Application;

public sealed class ScheduleCreateValidator : AbstractValidator<ScheduleCreateDto>
{
    public ScheduleCreateValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("The session must end after it starts.");
        RuleFor(x => x.MaxPatients).InclusiveBetween(1, DoctorSchedule.MaxPatientsLimit);
        RuleFor(x => x.AverageMinutesPerPatient).InclusiveBetween(1, 180);
        RuleFor(x => x.Room).MaximumLength(50);
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue)
            .WithMessage("The end date cannot be before the start date.");
    }
}

public sealed class ScheduleUpdateValidator : AbstractValidator<ScheduleUpdateDto>
{
    public ScheduleUpdateValidator()
    {
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("The session must end after it starts.");
        RuleFor(x => x.MaxPatients).InclusiveBetween(1, DoctorSchedule.MaxPatientsLimit);
        RuleFor(x => x.AverageMinutesPerPatient).InclusiveBetween(1, 180);
        RuleFor(x => x.Room).MaximumLength(50);
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue)
            .WithMessage("The end date cannot be before the start date.");
    }
}

public sealed class LeaveCreateValidator : AbstractValidator<LeaveCreateDto>
{
    public LeaveCreateValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class AvailabilityQueryValidator : AbstractValidator<AvailabilityQuery>
{
    public AvailabilityQueryValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
    }
}

public sealed class AppointmentBookValidator : AbstractValidator<AppointmentBookDto>
{
    public AppointmentBookValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.ScheduleId).NotEmpty();
        RuleFor(x => x.AppointmentDate).NotEmpty();
        RuleFor(x => x.VisitType).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class AppointmentRescheduleValidator : AbstractValidator<AppointmentRescheduleDto>
{
    public AppointmentRescheduleValidator()
    {
        RuleFor(x => x.ScheduleId).NotEmpty();
        RuleFor(x => x.AppointmentDate).NotEmpty();
    }
}

public sealed class AppointmentCancelValidator : AbstractValidator<AppointmentCancelDto>
{
    public AppointmentCancelValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
}

public sealed class AppointmentListQueryValidator : PaginationRequestValidator<AppointmentListQuery>
{
    public AppointmentListQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom!.Value).When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo must be on or after DateFrom.");
        RuleFor(x => x.SortBy).Empty().WithMessage("Appointments are sorted by date, session and serial.");
    }
}
