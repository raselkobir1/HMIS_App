using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Billing.Application;
using Hmis.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Billing.Infrastructure;

/// <summary>Owns the "billing" schema.</summary>
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "billing";

    public override string Schema => SchemaName;

    public override string ModuleName => "Billing";

    public DbSet<BillableService> Services => Set<BillableService>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<BillingSettings> Settings => Set<BillingSettings>();
}

public sealed class BillingUnitOfWork(BillingDbContext context)
    : UnitOfWork<BillingDbContext>(context), IBillingUnitOfWork;

public sealed class BillableServiceConfiguration : IEntityTypeConfiguration<BillableService>
{
    public void Configure(EntityTypeBuilder<BillableService> builder)
    {
        builder.ToTable("Services", t => t.HasCheckConstraint("CK_Services_Price", "\"Price\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.IsActive, x.Name });
    }
}

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", t =>
        {
            // Backstops for the domain rules: never pay more than the net amount, never discount below zero.
            t.HasCheckConstraint("CK_Invoices_Amounts",
                "\"Subtotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"NetAmount\" >= 0 AND \"PaidAmount\" >= 0 AND " +
                "\"AdvanceAppliedAmount\" >= 0 AND \"RefundedAmount\" >= 0");
            t.HasCheckConstraint("CK_Invoices_NotOverpaid", "\"PaidAmount\" <= \"NetAmount\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SourceType).HasMaxLength(30);
        builder.Property(x => x.SourceReference).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CancellationReason).HasMaxLength(400);
        foreach (var amount in new[] { nameof(Invoice.Subtotal), nameof(Invoice.DiscountAmount), nameof(Invoice.NetAmount),
                     nameof(Invoice.PaidAmount), nameof(Invoice.AdvanceAppliedAmount), nameof(Invoice.RefundedAmount) })
        {
            builder.Property<decimal>(amount).HasPrecision(18, 2);
        }

        builder.Ignore(x => x.DueAmount);
        builder.Ignore(x => x.CashHeldAmount);
        builder.Ignore(x => x.PaymentStatus);

        // One invoice per charge source and part (idempotent posting from other modules); part 2+ is a supplementary
        // bill opened for charges that arrive after the earlier part was finalized.
        builder.HasIndex(x => new { x.SourceType, x.SourceId, x.SourcePart }).IsUnique().HasFilter("\"SourceId\" IS NOT NULL");
        builder.HasIndex(x => new { x.PatientId, x.Status });
        builder.HasIndex(x => new { x.InvoiceDate, x.Status });

        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Discounts).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Discounts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines", t => t.HasCheckConstraint("CK_InvoiceLines_Values", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0"));
        builder.HasKey(x => x.Id);
        // Lines are added through the Invoice collection with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ServiceCode).HasMaxLength(20);
        builder.Property(x => x.ChargeKey).HasMaxLength(60);
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.InvoiceId);
        // A posted charge appears once per bill (idempotent posting); the domain skips known keys, this backs it up.
        builder.HasIndex(x => new { x.InvoiceId, x.ChargeKey }).IsUnique().HasFilter("\"ChargeKey\" IS NOT NULL");
    }
}

public sealed class InvoiceDiscountConfiguration : IEntityTypeConfiguration<InvoiceDiscount>
{
    public void Configure(EntityTypeBuilder<InvoiceDiscount> builder)
    {
        builder.ToTable("InvoiceDiscounts", t => t.HasCheckConstraint("CK_InvoiceDiscounts_Amount", "\"Amount\" > 0"));
        builder.HasKey(x => x.Id);
        // Added through the Invoice collection with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DecisionNote).HasMaxLength(250);
        builder.HasIndex(x => new { x.InvoiceId, x.Status });
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReceiptNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.ReceiptNumber).IsUnique();
        builder.HasIndex(x => x.ClientRequestId).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(250);
        builder.Ignore(x => x.IsAdvanceDeposit);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => x.ReceivedAt);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", t => t.HasCheckConstraint("CK_Refunds_Amount", "\"Amount\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RefundNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.RefundNumber).IsUnique();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DecisionNote).HasMaxLength(250);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Ignore(x => x.IsCommitted);
        builder.HasIndex(x => new { x.PatientId, x.Status });
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => new { x.Status, x.PaidAt });
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BillingSettingsConfiguration : IEntityTypeConfiguration<BillingSettings>
{
    public void Configure(EntityTypeBuilder<BillingSettings> builder)
    {
        builder.ToTable("BillingSettings", t => t.HasCheckConstraint("CK_BillingSettings_Discount",
            "\"DiscountApprovalPercent\" >= 0 AND \"DiscountApprovalPercent\" <= 100"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountApprovalPercent).HasPrecision(5, 2);
        builder.Property(x => x.DisabledPaymentMethods).HasMaxLength(200).IsRequired();
        builder.Ignore(x => x.Disabled);
    }
}

public sealed class BillingSettingsRepository(BillingDbContext context)
    : BaseRepository<BillingSettings, BillingDbContext>(context), IBillingSettingsRepository
{
    public Task<BillingSettings?> GetAsync(CancellationToken cancellationToken) => Set.FirstOrDefaultAsync(cancellationToken);
}

public sealed class BillableServiceRepository(BillingDbContext context)
    : BaseRepository<BillableService, BillingDbContext>(context), IBillableServiceRepository
{
    public async Task<PagedResponse<BillableService>> SearchAsync(ServiceQuery query, CancellationToken cancellationToken)
    {
        var list = Set.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            list = list.Where(s => EF.Functions.ILike(s.Name, pattern) || EF.Functions.ILike(s.Code, pattern));
        }

        if (query.Category.HasValue)
        {
            list = list.Where(s => s.Category == query.Category.Value);
        }

        if (query.IsActive.HasValue)
        {
            list = list.Where(s => s.IsActive == query.IsActive.Value);
        }

        var total = await list.LongCountAsync(cancellationToken);
        var items = await list.OrderBy(s => s.Name).Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<BillableService>.Create(items, total, query);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        Set.AnyAsync(s => s.Code == code, cancellationToken);

    public Task<BillableService?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        Set.AsNoTracking().FirstOrDefaultAsync(s => s.Code == code, cancellationToken);

    public async Task<IReadOnlyList<BillableService>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
}

public sealed class InvoiceRepository(BillingDbContext context)
    : BaseRepository<Invoice, BillingDbContext>(context), IInvoiceRepository
{
    public Task LockPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        Context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"billing:patient:{patientId:N}"}, 0))",
            cancellationToken);

    public Task<Guid?> GetPatientIdAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        Set.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => (Guid?)i.PatientId).FirstOrDefaultAsync(cancellationToken);

    public Task<Invoice?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        WithDetails(Set).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Invoice?> GetByDiscountIdAsync(Guid discountId, CancellationToken cancellationToken) =>
        Set.AsNoTracking().FirstOrDefaultAsync(i => i.Discounts.Any(d => d.Id == discountId), cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Include(i => i.Lines)
            .Where(i => i.SourceType == sourceType && i.SourceId == sourceId)
            .OrderBy(i => i.SourcePart)
            .ToListAsync(cancellationToken);

    public async Task<PagedResponse<Invoice>> SearchAsync(InvoiceListQuery query, CancellationToken cancellationToken)
    {
        var list = Set.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            list = list.Where(i => EF.Functions.ILike(i.InvoiceNumber, pattern) || EF.Functions.ILike(i.SourceReference!, pattern));
        }

        if (query.PatientId.HasValue)
        {
            list = list.Where(i => i.PatientId == query.PatientId.Value);
        }

        if (query.Status.HasValue)
        {
            list = list.Where(i => i.Status == query.Status.Value);
        }

        if (query.DueOnly == true)
        {
            list = list.Where(i => i.Status == InvoiceStatus.Finalized && i.PaidAmount < i.NetAmount);
        }

        if (query.DateFrom.HasValue)
        {
            list = list.Where(i => i.InvoiceDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            list = list.Where(i => i.InvoiceDate <= query.DateTo.Value);
        }

        var total = await list.LongCountAsync(cancellationToken);
        var items = await list.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt)
            .Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<Invoice>.Create(items, total, query);
    }

    public async Task<IReadOnlyList<Invoice>> GetForPatientAsync(Guid patientId, int take, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(i => ids.Contains(i.Id)).ToListAsync(cancellationToken);

    public Task<decimal> GetOutstandingAsync(Guid patientId, CancellationToken cancellationToken) =>
        Set.Where(i => i.PatientId == patientId && i.Status == InvoiceStatus.Finalized).SumAsync(i => i.NetAmount - i.PaidAmount, cancellationToken);

    public Task<bool> HasDraftAsync(Guid patientId, CancellationToken cancellationToken) =>
        Set.AnyAsync(i => i.PatientId == patientId && i.Status == InvoiceStatus.Draft, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetWithPendingDiscountsAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Include(i => i.Discounts)
            .Where(i => i.Discounts.Any(d => d.Status == ApprovalStatus.Pending))
            .ToListAsync(cancellationToken);

    private static IQueryable<Invoice> WithDetails(IQueryable<Invoice> query) =>
        query.Include(i => i.Lines).Include(i => i.Discounts).AsSplitQuery();
}

public sealed class PaymentRepository(BillingDbContext context)
    : BaseRepository<Payment, BillingDbContext>(context), IPaymentRepository
{
    public Task<Payment?> GetByClientRequestAsync(Guid clientRequestId, CancellationToken cancellationToken) =>
        Set.AsNoTracking().FirstOrDefaultAsync(p => p.ClientRequestId == clientRequestId, cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(p => p.InvoiceId == invoiceId).OrderBy(p => p.ReceivedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetForPatientAsync(Guid patientId, int take, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(p => p.PatientId == patientId).OrderByDescending(p => p.ReceivedAt).Take(take).ToListAsync(cancellationToken);

    public async Task<decimal> GetAdvanceBalanceAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var deposits = await Set.Where(p => p.PatientId == patientId && p.InvoiceId == null).SumAsync(p => p.Amount, cancellationToken);
        if (deposits == 0)
        {
            return 0;
        }

        // Advance applied to an invoice that was later cancelled is released back to the balance.
        var applied = await Set
            .Where(p => p.PatientId == patientId && p.Method == PaymentMethod.Advance &&
                        Context.Invoices.Any(i => i.Id == p.InvoiceId && i.Status != InvoiceStatus.Cancelled))
            .SumAsync(p => p.Amount, cancellationToken);
        var returned = await Context.Refunds
            .Where(r => r.PatientId == patientId && r.Type == RefundType.AdvanceReturn && r.Status != RefundStatus.Rejected)
            .SumAsync(r => r.Amount, cancellationToken);
        return deposits - applied - returned;
    }

    public async Task<IReadOnlyList<Payment>> GetReceivedBetweenAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(p => p.ReceivedAt >= fromUtc && p.ReceivedAt < toUtc && (userId == null || p.CreatedBy == userId))
            .OrderBy(p => p.ReceivedAt)
            .ToListAsync(cancellationToken);
}

public sealed class RefundRepository(BillingDbContext context)
    : BaseRepository<Refund, BillingDbContext>(context), IRefundRepository
{
    public Task<Guid?> GetPatientIdAsync(Guid refundId, CancellationToken cancellationToken) =>
        Set.AsNoTracking().Where(r => r.Id == refundId).Select(r => (Guid?)r.PatientId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Refund>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(r => r.InvoiceId == invoiceId).OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Refund>> GetForPatientAsync(Guid patientId, int take, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(r => r.PatientId == patientId).OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Refund>> GetByStatusAsync(RefundStatus status, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(r => r.Status == status).OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Refund>> GetPaidBetweenAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(r => r.Status == RefundStatus.Paid && r.PaidAt >= fromUtc && r.PaidAt < toUtc && (userId == null || r.PaidBy == userId))
            .OrderBy(r => r.PaidAt)
            .ToListAsync(cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<BillingDbContext>(configuration, BillingDbContext.SchemaName);
        services.AddScoped<IBillingUnitOfWork, BillingUnitOfWork>();
        services.AddScoped<IBillableServiceRepository, BillableServiceRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IBillingSettingsRepository, BillingSettingsRepository>();
        services.AddScoped<IBillingSettingsManager, BillingSettingsManager>();
        services.AddScoped<BillingContractServices>();
        services.AddScoped<IServicePriceLookup>(sp => sp.GetRequiredService<BillingContractServices>());
        services.AddScoped<IPatientMergeGuard>(sp => sp.GetRequiredService<BillingContractServices>());
        services.AddScoped<IStartupTask, BillingSeedTask>();
        services.AddScoped<BillingDtoFactory>();
        services.AddScoped<IServiceCatalogManager, ServiceCatalogManager>();
        services.AddScoped<InvoiceManager>();
        services.AddScoped<IInvoiceManager>(sp => sp.GetRequiredService<InvoiceManager>());
        services.AddScoped<IChargeService>(sp => sp.GetRequiredService<InvoiceManager>());
        services.AddScoped<PaymentManager>();
        services.AddScoped<IPaymentManager>(sp => sp.GetRequiredService<PaymentManager>());
        services.AddScoped<IPatientBalanceLookup>(sp => sp.GetRequiredService<PaymentManager>());
        services.AddSingleton<IPermissionDefinitionProvider, BillingPermissionDefinitionProvider>();
        services.AddSingleton<INumberSequenceDefinitionProvider, BillingNumberSequences>();
        return services;
    }
}
