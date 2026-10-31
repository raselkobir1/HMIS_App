using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Files;
using Hmis.BuildingBlocks.Infrastructure.Files;
using Hmis.BuildingBlocks.Infrastructure.Auditing;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Hmis.BuildingBlocks.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "HmisDatabase";

    public static IServiceCollection AddBuildingBlocksInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<PendingAuditEvents>();
        services.AddScoped<AuditRecordFactory>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        return services;
    }

    /// <summary>
    /// Registers a module DbContext on the shared PostgreSQL database with the migrations-history table
    /// inside the module's own schema, plus the module's Unit of Work.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : ModuleDbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(TContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            });
            // Order matters: stamp Created/Modified first, then capture the audit snapshot.
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<UnitOfWork<TContext>>();
        return services;
    }
}
