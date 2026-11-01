using Hmis.BuildingBlocks.Contracts.Ipd;
using Hmis.Modules.Ipd.Domain.Entities;

namespace Hmis.Modules.Ipd.Application;

/// <summary>Inpatient stays for Clinical (progress notes, medication orders, vitals) and Nursing (MAR, notes, handover).</summary>
public sealed class InpatientLookup(IAdmissionRepository admissions, IBedRepository beds, IWardRepository wards) : IInpatientLookup
{
    public async Task<InpatientStay?> GetAsync(Guid admissionId, CancellationToken cancellationToken)
    {
        var admission = await admissions.GetWithBedsAsync(admissionId, cancellationToken);
        return admission is null ? null : (await ToStaysAsync([admission], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<InpatientStay>> GetActiveAsync(Guid? wardId, CancellationToken cancellationToken)
    {
        var stays = await ToStaysAsync(await admissions.GetActiveAsync(cancellationToken), cancellationToken);
        return stays.Where(s => wardId is null || s.WardId == wardId)
            .OrderBy(s => s.WardName).ThenBy(s => s.BedNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<bool> IsAttendingDoctorAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken) =>
        admissions.IsAttendingDoctorAsync(doctorId, patientId, cancellationToken);

    public async Task<IReadOnlyList<WardSummary>> GetWardsAsync(CancellationToken cancellationToken) =>
        (await wards.GetAllAsync(cancellationToken)).Select(w => new WardSummary(w.Id, w.Code, w.Name, w.IsActive)).ToList();

    private async Task<IReadOnlyList<InpatientStay>> ToStaysAsync(IReadOnlyList<Admission> list, CancellationToken cancellationToken)
    {
        // The current bed, or the last one for a discharged patient.
        var lastBeds = list.ToDictionary(a => a.Id, a => (a.CurrentBed ?? a.Beds.OrderBy(b => b.StartedAt).LastOrDefault())?.BedId);
        var bedList = await beds.GetByIdsAsync(lastBeds.Values.OfType<Guid>().Distinct().ToList(), cancellationToken);
        var bedMap = bedList.ToDictionary(b => b.Id);
        var wardMap = (await wards.GetByIdsAsync(bedList.Select(b => b.WardId).Distinct().ToList(), cancellationToken)).ToDictionary(w => w.Id);

        return list.Select(a =>
        {
            var bed = lastBeds[a.Id] is { } bedId ? bedMap.GetValueOrDefault(bedId) : null;
            var ward = bed is null ? null : wardMap.GetValueOrDefault(bed.WardId);
            return new InpatientStay(a.Id, a.AdmissionNumber, a.PatientId, a.AttendingDoctorId, a.Status.ToString(), a.IsActive, a.AdmittedAt,
                a.ReleasedAt, ward?.Id, ward?.Name, bed?.BedNumber);
        }).ToList();
    }
}
