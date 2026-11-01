using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Contracts.Patient;

namespace Hmis.Modules.Appointment.Application;

/// <summary>A duplicate record with upcoming appointments is not merged: cancel them or rebook on the surviving record first.</summary>
public sealed class AppointmentMergeGuard(IAppointmentRepository appointments, IDateTimeProvider dateTime) : IPatientMergeGuard
{
    public async Task<IReadOnlyList<string>> GetMergeBlockersAsync(Guid patientId, CancellationToken cancellationToken) =>
        await appointments.CountOpenForPatientAsync(patientId, dateTime.LocalToday, cancellationToken) is > 0 and var count
            ? [$"{count} upcoming appointment{(count == 1 ? "" : "s")}"]
            : [];
}
