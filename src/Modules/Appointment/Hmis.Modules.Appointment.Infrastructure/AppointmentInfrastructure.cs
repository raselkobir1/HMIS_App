using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Appointment;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Appointment.Application;
using Hmis.Modules.Appointment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AppointmentEntity = Hmis.Modules.Appointment.Domain.Entities.Appointment;

namespace Hmis.Modules.Appointment.Infrastructure;

/// <summary>Owns the "appointment" schema.</summary>
public sealed class AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "appointment";

    public override string Schema => SchemaName;

    public override string ModuleName => "Appointment";

    public DbSet<DoctorSchedule> Schedules => Set<DoctorSchedule>();
    public DbSet<DoctorLeave> Leaves => Set<DoctorLeave>();
    public DbSet<AppointmentEntity> Appointments => Set<AppointmentEntity>();
}

public sealed class AppointmentUnitOfWork(AppointmentDbContext context)
    : UnitOfWork<AppointmentDbContext>(context), IAppointmentUnitOfWork;

public sealed class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
    {
        builder.ToTable("DoctorSchedules", t =>
        {
            t.HasCheckConstraint("CK_DoctorSchedules_TimeRange", "\"EndTime\" > \"StartTime\"");
            t.HasCheckConstraint("CK_DoctorSchedules_MaxPatients", "\"MaxPatients\" BETWEEN 1 AND 200");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Room).HasMaxLength(50);
        builder.HasIndex(x => new { x.DoctorId, x.DayOfWeek, x.IsActive });
    }
}

public sealed class DoctorLeaveConfiguration : IEntityTypeConfiguration<DoctorLeave>
{
    public void Configure(EntityTypeBuilder<DoctorLeave> builder)
    {
        builder.ToTable("DoctorLeaves");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
        // One leave entry per doctor/date/session (NULL session = whole day counts as a value).
        builder.HasIndex(x => new { x.DoctorId, x.Date, x.ScheduleId }).IsUnique().AreNullsDistinct(false);
        builder.HasOne<DoctorSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<AppointmentEntity>
{
    public void Configure(EntityTypeBuilder<AppointmentEntity> builder)
    {
        builder.ToTable("Appointments", t => t.HasCheckConstraint("CK_Appointments_Fee", "\"Fee\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AppointmentNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.AppointmentNumber).IsUnique();

        // A serial (queue token) is unique within a session day; the advisory lock prevents races, this backs it up.
        builder.HasIndex(x => new { x.ScheduleId, x.AppointmentDate, x.SerialNumber }).IsUnique();
        builder.HasIndex(x => new { x.DoctorId, x.AppointmentDate });
        builder.HasIndex(x => new { x.PatientId, x.AppointmentDate });
        builder.HasIndex(x => new { x.AppointmentDate, x.Status });

        builder.Property(x => x.VisitType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Fee).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CancellationReason).HasMaxLength(250);
        builder.HasOne<DoctorSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ScheduleRepository(AppointmentDbContext context)
    : BaseRepository<DoctorSchedule, AppointmentDbContext>(context), IScheduleRepository
{
    public async Task<IReadOnlyList<DoctorSchedule>> SearchAsync(ScheduleQuery query, CancellationToken cancellationToken)
    {
        var list = Set.AsNoTracking();
        if (query.DoctorId.HasValue)
        {
            list = list.Where(s => s.DoctorId == query.DoctorId.Value);
        }

        if (query.IsActive.HasValue)
        {
            list = list.Where(s => s.IsActive == query.IsActive.Value);
        }

        return (await list.ToListAsync(cancellationToken))
            .OrderBy(s => s.DoctorId).ThenBy(s => ((int)s.DayOfWeek + 1) % 7).ThenBy(s => s.StartTime) // Saturday-first week
            .ToList();
    }

    public async Task<IReadOnlyList<DoctorSchedule>> GetActiveForDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(s => s.DoctorId == doctorId && s.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorSchedule>> GetRunningOnAsync(DateOnly date, Guid? doctorId, CancellationToken cancellationToken)
    {
        var day = date.DayOfWeek;
        var list = Set.AsNoTracking().Where(s => s.IsActive && s.DayOfWeek == day && s.EffectiveFrom <= date &&
                                                 (s.EffectiveTo == null || s.EffectiveTo >= date));
        if (doctorId.HasValue)
        {
            list = list.Where(s => s.DoctorId == doctorId.Value);
        }

        return await list.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DoctorSchedule>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
}

public sealed class LeaveRepository(AppointmentDbContext context)
    : BaseRepository<DoctorLeave, AppointmentDbContext>(context), ILeaveRepository
{
    public async Task<IReadOnlyList<DoctorLeave>> GetForDateAsync(DateOnly date, Guid? doctorId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(l => l.Date == date && (doctorId == null || l.DoctorId == doctorId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorLeave>> SearchAsync(LeaveQuery query, CancellationToken cancellationToken)
    {
        var list = Set.AsNoTracking();
        if (query.DoctorId.HasValue)
        {
            list = list.Where(l => l.DoctorId == query.DoctorId.Value);
        }

        if (query.From.HasValue)
        {
            list = list.Where(l => l.Date >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            list = list.Where(l => l.Date <= query.To.Value);
        }

        return await list.OrderBy(l => l.Date).Take(500).ToListAsync(cancellationToken);
    }
}

public sealed class AppointmentRepository(AppointmentDbContext context)
    : BaseRepository<AppointmentEntity, AppointmentDbContext>(context), IAppointmentRepository
{
    private static readonly AppointmentStatus[] Active = AppointmentEntity.ActiveStatuses;
    private static readonly AppointmentStatus[] Pending = [AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.Waiting];

    public Task LockSessionDayAsync(Guid scheduleId, DateOnly date, CancellationToken cancellationToken) =>
        Context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"appointment:{scheduleId:N}:{date:yyyyMMdd}"}, 0))",
            cancellationToken);

    public async Task<SessionCount> GetSessionCountAsync(Guid scheduleId, DateOnly date, CancellationToken cancellationToken)
    {
        var day = Set.Where(a => a.ScheduleId == scheduleId && a.AppointmentDate == date);
        var booked = await day.CountAsync(a => Active.Contains(a.Status), cancellationToken);
        var maxSerial = await day.MaxAsync(a => (int?)a.SerialNumber, cancellationToken) ?? 0;
        return new SessionCount(booked, maxSerial);
    }

    public async Task<IReadOnlyDictionary<Guid, SessionCount>> GetSessionCountsAsync(
        IReadOnlyCollection<Guid> scheduleIds, DateOnly date, CancellationToken cancellationToken)
    {
        var rows = await Set.AsNoTracking()
            .Where(a => scheduleIds.Contains(a.ScheduleId) && a.AppointmentDate == date)
            .GroupBy(a => a.ScheduleId)
            .Select(g => new { g.Key, Booked = g.Count(a => Active.Contains(a.Status)), MaxSerial = g.Max(a => a.SerialNumber) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Key, r => new SessionCount(r.Booked, r.MaxSerial));
    }

    public Task<bool> PatientHasActiveWithDoctorAsync(Guid patientId, Guid doctorId, DateOnly date, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(a => a.PatientId == patientId && a.DoctorId == doctorId && a.AppointmentDate == date &&
                          Active.Contains(a.Status) && a.Id != excludeId, cancellationToken);

    public async Task<PagedResponse<AppointmentEntity>> SearchAsync(AppointmentListQuery query, CancellationToken cancellationToken)
    {
        var list = Set.AsNoTracking();
        if (query.Date.HasValue)
        {
            list = list.Where(a => a.AppointmentDate == query.Date.Value);
        }

        if (query.DateFrom.HasValue)
        {
            list = list.Where(a => a.AppointmentDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            list = list.Where(a => a.AppointmentDate <= query.DateTo.Value);
        }

        if (query.DoctorId.HasValue)
        {
            list = list.Where(a => a.DoctorId == query.DoctorId.Value);
        }

        if (query.PatientIds is { Count: > 0 } patientIds)
        {
            list = list.Where(a => patientIds.Contains(a.PatientId));
        }
        else if (query.PatientId.HasValue)
        {
            list = list.Where(a => a.PatientId == query.PatientId.Value);
        }

        if (query.Status.HasValue)
        {
            list = list.Where(a => a.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            list = list.Where(a => a.AppointmentNumber.StartsWith(term));
        }

        // Single-day views read like a queue; date ranges (e.g. a patient's history) show newest first.
        list = query.Date.HasValue
            ? list.OrderBy(a => a.EstimatedTime).ThenBy(a => a.SerialNumber)
            : list.OrderByDescending(a => a.AppointmentDate).ThenBy(a => a.EstimatedTime);

        var total = await list.LongCountAsync(cancellationToken);
        var items = await list.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<AppointmentEntity>.Create(items, total, query);
    }

    public async Task<IReadOnlyList<AppointmentEntity>> GetForDateAsync(DateOnly date, Guid? doctorId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(a => a.AppointmentDate == date && (doctorId == null || a.DoctorId == doctorId))
            .OrderBy(a => a.SerialNumber)
            .ToListAsync(cancellationToken);

    public Task<bool> HasConsultationInProgressAsync(Guid doctorId, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.InConsultation && a.Id != excludeId, cancellationToken);

    public async Task<FutureUsage> GetFutureUsageAsync(Guid scheduleId, DateOnly fromDate, CancellationToken cancellationToken)
    {
        var perDay = await Set.AsNoTracking()
            .Where(a => a.ScheduleId == scheduleId && a.AppointmentDate >= fromDate && Active.Contains(a.Status))
            .GroupBy(a => a.AppointmentDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return perDay.Count == 0
            ? new FutureUsage(0, 0, null)
            : new FutureUsage(perDay.Sum(d => d.Count), perDay.Max(d => d.Count), perDay.Max(d => d.Date));
    }

    public Task<int> CountOpenForPatientAsync(Guid patientId, DateOnly fromDate, CancellationToken cancellationToken) =>
        Set.CountAsync(a => a.PatientId == patientId && a.AppointmentDate >= fromDate &&
                            (Pending.Contains(a.Status) || a.Status == AppointmentStatus.InConsultation), cancellationToken);

    public Task<bool> HasRelationshipAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken) =>
        Set.AnyAsync(a => a.DoctorId == doctorId && a.PatientId == patientId && a.Status != AppointmentStatus.Cancelled, cancellationToken);

    public Task<int> CountPendingAsync(Guid doctorId, DateOnly date, Guid? scheduleId, CancellationToken cancellationToken) =>
        Set.CountAsync(a => a.DoctorId == doctorId && a.AppointmentDate == date && Pending.Contains(a.Status) &&
                            (scheduleId == null || a.ScheduleId == scheduleId), cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddAppointmentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AppointmentDbContext>(configuration, AppointmentDbContext.SchemaName);
        services.AddScoped<IAppointmentUnitOfWork, AppointmentUnitOfWork>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<ILeaveRepository, LeaveRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IScheduleManager, ScheduleManager>();
        services.AddScoped<AppointmentManager>();
        services.AddScoped<IAppointmentManager>(sp => sp.GetRequiredService<AppointmentManager>());
        services.AddScoped<IOpdEncounterService>(sp => sp.GetRequiredService<AppointmentManager>());
        services.AddScoped<Hmis.BuildingBlocks.Contracts.Patient.IPatientMergeGuard, AppointmentMergeGuard>();
        services.AddSingleton<IPermissionDefinitionProvider, AppointmentPermissionDefinitionProvider>();
        return services;
    }
}
