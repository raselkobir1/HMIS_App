using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Contracts.Patient;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Application;

/// <summary>
/// Contracts Patient provides to other modules. Kept apart from <see cref="PatientManager"/>, which itself uses Billing
/// (registration fee) while Billing uses this lookup.
/// </summary>
public sealed class PatientContractServices(
    IPatientRepository repository,
    IPatientUnitOfWork unitOfWork,
    IAuditService auditService) : IPatientLookup, IPatientStatusService
{
    /// <summary>Merges can chain (A into B, then B into C), so this walks every level.</summary>
    internal static async Task<IReadOnlyList<PatientEntity>> CollectMergedAsync(IPatientRepository repository, Guid patientId,
        CancellationToken cancellationToken)
    {
        var all = new List<PatientEntity>();
        var frontier = new List<Guid> { patientId };
        while (frontier.Count > 0 && all.Count < 100)
        {
            var next = await repository.GetMergedIntoAsync(frontier, cancellationToken);
            all.AddRange(next);
            frontier = next.Select(p => p.Id).ToList();
        }

        return all;
    }

    public async Task<PatientSummary?> GetAsync(Guid patientId, CancellationToken cancellationToken) =>
        ToSummary(await repository.GetByIdAsync(patientId, cancellationToken));

    public async Task<PatientSummary?> GetByMRNumberAsync(string mrNumber, CancellationToken cancellationToken) =>
        ToSummary(await repository.GetByMRNumberAsync(mrNumber.Trim().ToUpperInvariant(), cancellationToken));

    public async Task<IReadOnlyList<PatientSummary>> GetManyAsync(IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken) =>
        (await repository.GetByIdsAsync(patientIds, cancellationToken)).Select(p => ToSummary(p)!).ToList();

    public async Task<IReadOnlyList<Guid>> GetWithMergedAsync(Guid patientId, CancellationToken cancellationToken) =>
        (await CollectMergedAsync(repository, patientId, cancellationToken)).Select(p => p.Id).Prepend(patientId).ToList();

    public async Task MarkDeceasedAsync(Guid patientId, DateTime diedAt, string source, CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(patientId, cancellationToken) is not { } patient
            || patient.Status is Domain.Entities.PatientStatus.Deceased or Domain.Entities.PatientStatus.Merged)
        {
            return;
        }

        patient.MarkDeceased(diedAt);
        auditService.Record(new AuditEvent("MarkDeceased", PatientPermissions.Module, "Patient", patientId.ToString(), NewValues: new { source, diedAt }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static PatientSummary? ToSummary(PatientEntity? p) =>
        p is null
            ? null
            : new PatientSummary(p.Id, p.MRNumber, p.FullName, p.Gender.ToString(), p.DateOfBirth, p.MobileNumber,
                p.Status.ToString(), p.MergedIntoPatientId);
}
