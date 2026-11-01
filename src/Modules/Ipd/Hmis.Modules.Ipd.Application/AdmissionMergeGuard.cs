using Hmis.BuildingBlocks.Contracts.Patient;

namespace Hmis.Modules.Ipd.Application;

/// <summary>A duplicate record with a stay in progress is not merged: discharge the patient first.</summary>
public sealed class AdmissionMergeGuard(IAdmissionRepository admissions) : IPatientMergeGuard
{
    public async Task<IReadOnlyList<string>> GetMergeBlockersAsync(Guid patientId, CancellationToken cancellationToken) =>
        (await admissions.GetActiveAsync(cancellationToken)).Where(a => a.PatientId == patientId)
            .Select(a => $"admitted ({a.AdmissionNumber}, not yet discharged)")
            .ToList();
}
