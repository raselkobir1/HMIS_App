using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.HR.Application;
using Hmis.Modules.HR.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hmis.Modules.HR.Infrastructure;

public sealed class DoctorProfileConfiguration : IEntityTypeConfiguration<DoctorProfile>
{
    public void Configure(EntityTypeBuilder<DoctorProfile> builder)
    {
        builder.ToTable("DoctorProfiles");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.EmployeeId).IsUnique();
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Specialization).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Qualifications).HasMaxLength(250).IsRequired();
        builder.Property(x => x.RegistrationNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.RegistrationNumber).IsUnique();
        builder.Property(x => x.NewPatientFee).HasPrecision(18, 2);
        builder.Property(x => x.FollowUpFee).HasPrecision(18, 2);
        builder.Property(x => x.InpatientVisitFee).HasPrecision(18, 2);
    }
}

public sealed class DoctorProfileRepository(HrDbContext context)
    : BaseRepository<DoctorProfile, HrDbContext>(context), IDoctorProfileRepository
{
    public Task<DoctorProfile?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(d => d.EmployeeId == employeeId, cancellationToken);

    public Task<bool> RegistrationExistsAsync(string registrationNumber, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(d => d.RegistrationNumber == registrationNumber && d.Id != excludeId, cancellationToken);

    public async Task<PagedResponse<(DoctorProfile Profile, Employee Employee)>> SearchAsync(DoctorListQuery query, CancellationToken cancellationToken)
    {
        var rows = Set.AsNoTracking().Join(Context.Employees.AsNoTracking(), d => d.EmployeeId, e => e.Id, (d, e) => new { d, e });

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            rows = rows.Where(r => EF.Functions.ILike(r.e.FullName, pattern) || EF.Functions.ILike(r.d.Specialization, pattern)
                || EF.Functions.ILike(r.e.EmployeeCode, pattern));
        }

        if (query.DepartmentId.HasValue)
        {
            rows = rows.Where(r => r.e.DepartmentId == query.DepartmentId.Value);
        }

        if (query.IsAcceptingAppointments.HasValue)
        {
            rows = rows.Where(r => r.d.IsAcceptingAppointments == query.IsAcceptingAppointments.Value);
        }

        var total = await rows.LongCountAsync(cancellationToken);
        var items = await rows.OrderBy(r => r.e.FullName).Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<(DoctorProfile, Employee)>.Create(items.Select(r => (r.d, r.e)).ToList(), total, query);
    }

    public async Task<IReadOnlyList<(DoctorProfile Profile, Employee Employee)>> GetByEmployeeIdsAsync(
        IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking()
            .Where(d => employeeIds.Contains(d.EmployeeId))
            .Join(Context.Employees.AsNoTracking(), d => d.EmployeeId, e => e.Id, (d, e) => new { d, e })
            .ToListAsync(cancellationToken))
        .Select(r => (r.d, r.e))
        .ToList();
}
