using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Patient.Application;
using Hmis.Modules.Patient.Domain.Entities;
using Hmis.Modules.Patient.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Infrastructure.Repositories;

public sealed class PatientRepository(PatientDbContext context)
    : BaseRepository<PatientEntity, PatientDbContext>(context), IPatientRepository
{
    // Missing inputs are NULL parameters, so every match flag is COALESCEd to false (SQL three-valued logic).
    // Trigram similarity thresholds (0..1). Lower for same-mobile matches ("Rahim" vs "Md. Rahim Uddin"),
    // higher when only date of birth and gender agree.
    private const double SameMobileNameThreshold = 0.3;
    private const double SameBirthNameThreshold = 0.45;

    private const string DuplicateSql = """
        SELECT "Id", "MRNumber", "FullName", "Gender", "DateOfBirth", "MobileNumber",
               COALESCE("IdentificationNumber" IS NOT NULL AND "IdentificationType" = @idType AND "IdentificationNumber" = @idNumber, false) AS "SameIdentification",
               COALESCE("MobileNumber" = @mobile AND similarity("FullName", @name) >= @mobileThreshold, false) AS "SameMobileSimilarName",
               COALESCE("DateOfBirth" = @dob AND "Gender" = @gender AND similarity("FullName", @name) >= @birthThreshold, false) AS "SameBirthSimilarName",
               COALESCE(similarity("FullName", @name), 0)::float8 AS "NameSimilarity"
        FROM patient."Patients"
        WHERE "Status" <> 'Merged' AND "Id" <> @excludeId
          AND (
                ("IdentificationNumber" IS NOT NULL AND "IdentificationType" = @idType AND "IdentificationNumber" = @idNumber)
             OR ("MobileNumber" = @mobile AND similarity("FullName", @name) >= @mobileThreshold)
             OR ("DateOfBirth" = @dob AND "Gender" = @gender AND similarity("FullName", @name) >= @birthThreshold)
          )
        ORDER BY "SameIdentification" DESC, "NameSimilarity" DESC
        LIMIT 10
        """;

    public Task<PatientEntity?> GetByMRNumberAsync(string mrNumber, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(p => p.MRNumber == mrNumber, cancellationToken);

    public async Task<IReadOnlyList<PatientEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

    public Task<bool> IdentificationExistsAsync(
        IdentificationType type, string normalizedNumber, Guid? excludePatientId, CancellationToken cancellationToken) =>
        Set.AnyAsync(p => p.IdentificationType == type && p.IdentificationNumber == normalizedNumber && p.Id != excludePatientId,
            cancellationToken);

    public async Task<PagedResponse<PatientEntity>> SearchAsync(PatientSearchQuery query, CancellationToken cancellationToken)
    {
        var patients = Set.AsNoTracking();

        if (query.Status.HasValue)
        {
            patients = patients.Where(p => p.Status == query.Status.Value);
        }

        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var upper = term.ToUpperInvariant();
            var pattern = $"%{term}%";
            var idNumber = PatientEntity.NormalizeIdentification(term);
            var digits = new string(term.Where(char.IsDigit).ToArray());

            patients = patients.Where(p =>
                p.MRNumber.StartsWith(upper) ||
                EF.Functions.ILike(p.FullName, pattern) ||
                (digits.Length >= 4 && p.MobileNumber.Contains(digits)) ||
                (idNumber != null && p.IdentificationNumber == idNumber));

            // Exact MR number first, then alphabetical.
            patients = patients.OrderByDescending(p => p.MRNumber == upper).ThenBy(p => p.FullName).ThenBy(p => p.MRNumber);
        }
        else
        {
            patients = patients.OrderByDescending(p => p.CreatedAt);
        }

        var total = await patients.LongCountAsync(cancellationToken);
        var items = await patients.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<PatientEntity>.Create(items, total, query);
    }

    public async Task<IReadOnlyList<DuplicateCandidateRow>> FindDuplicateCandidatesAsync(DuplicateCheckQuery query, CancellationToken cancellationToken)
    {
        // Typed parameters: PostgreSQL cannot infer types for NULL values on its own.
        NpgsqlParameter[] parameters =
        [
            new("name", NpgsqlDbType.Text) { Value = (object?)query.FullName?.Trim() ?? DBNull.Value },
            new("mobile", NpgsqlDbType.Varchar) { Value = (object?)query.MobileNumber?.Trim() ?? DBNull.Value },
            new("dob", NpgsqlDbType.Date) { Value = (object?)query.DateOfBirth ?? DBNull.Value },
            new("gender", NpgsqlDbType.Varchar) { Value = (object?)query.Gender?.ToString() ?? DBNull.Value },
            new("idType", NpgsqlDbType.Varchar) { Value = (object?)query.IdentificationType?.ToString() ?? DBNull.Value },
            new("idNumber", NpgsqlDbType.Varchar) { Value = (object?)query.IdentificationNumber ?? DBNull.Value },
            new("excludeId", NpgsqlDbType.Uuid) { Value = query.ExcludePatientId ?? Guid.Empty },
            new("mobileThreshold", NpgsqlDbType.Real) { Value = (float)SameMobileNameThreshold },
            new("birthThreshold", NpgsqlDbType.Real) { Value = (float)SameBirthNameThreshold }
        ];

        var rows = await Context.Database.SqlQueryRaw<DuplicateSqlRow>(DuplicateSql, parameters).ToListAsync(cancellationToken);

        return rows.Select(r => new DuplicateCandidateRow(
            r.Id, r.MRNumber, r.FullName, Enum.Parse<Gender>(r.Gender), r.DateOfBirth, r.MobileNumber,
            r.SameIdentification, r.SameMobileSimilarName, r.SameBirthSimilarName, r.NameSimilarity)).ToList();
    }

    private sealed class DuplicateSqlRow
    {
        public Guid Id { get; set; }
        public string MRNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
        public string MobileNumber { get; set; } = string.Empty;
        public bool SameIdentification { get; set; }
        public bool SameMobileSimilarName { get; set; }
        public bool SameBirthSimilarName { get; set; }
        public double NameSimilarity { get; set; }
    }
}

public sealed class PatientAllergyRepository(PatientDbContext context)
    : BaseRepository<PatientAllergy, PatientDbContext>(context), IPatientAllergyRepository
{
    public async Task<IReadOnlyList<PatientAllergy>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(a => a.PatientId == patientId)
            .OrderBy(a => a.Status == AllergyStatus.Active ? 0 : a.Status == AllergyStatus.Resolved ? 1 : 2)
            .ThenBy(a => a.Severity == AllergySeverity.Severe ? 0 : a.Severity == AllergySeverity.Moderate ? 1 : 2)
            .ThenBy(a => a.Allergen)
            .ToListAsync(cancellationToken);
}

public sealed class PatientMedicalHistoryRepository(PatientDbContext context)
    : BaseRepository<PatientMedicalHistory, PatientDbContext>(context), IPatientMedicalHistoryRepository
{
    public async Task<IReadOnlyList<PatientMedicalHistory>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(h => h.PatientId == patientId)
            .OrderBy(h => h.IsEnteredInError).ThenBy(h => h.Category).ThenByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
}

public sealed class PatientDocumentRepository(PatientDbContext context)
    : BaseRepository<PatientDocument, PatientDbContext>(context), IPatientDocumentRepository
{
    public async Task<IReadOnlyList<PatientDocument>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(d => d.PatientId == patientId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
}

public sealed class PatientMergeRequestRepository(PatientDbContext context)
    : BaseRepository<PatientMergeRequest, PatientDbContext>(context), IPatientMergeRequestRepository
{
    public Task<bool> HasPendingForSourceAsync(Guid sourcePatientId, CancellationToken cancellationToken) =>
        Set.AnyAsync(r => r.SourcePatientId == sourcePatientId && r.Status == MergeRequestStatus.Pending, cancellationToken);

    public async Task<PagedResponse<PatientMergeRequest>> SearchAsync(MergeRequestQuery query, CancellationToken cancellationToken)
    {
        var requests = Set.AsNoTracking();
        if (query.Status.HasValue)
        {
            requests = requests.Where(r => r.Status == query.Status.Value);
        }

        requests = requests.OrderByDescending(r => r.CreatedAt);
        var total = await requests.LongCountAsync(cancellationToken);
        var items = await requests.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<PatientMergeRequest>.Create(items, total, query);
    }
}
