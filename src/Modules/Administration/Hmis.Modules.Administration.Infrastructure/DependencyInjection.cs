using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Application.Managers;
using Hmis.Modules.Administration.Application.Security;
using Hmis.Modules.Administration.Application.Services;
using Hmis.Modules.Administration.Infrastructure.Persistence;
using Hmis.Modules.Administration.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Administration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAdministrationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AdministrationDbContext>(configuration, AdministrationDbContext.SchemaName);
        services.AddScoped<IAdministrationUnitOfWork, AdministrationUnitOfWork>();
        services.Configure<AdministrationSeedOptions>(configuration.GetSection(AdministrationSeedOptions.SectionName));

        // Repositories
        services.AddScoped<IHospitalProfileRepository, HospitalProfileRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IDesignationRepository, DesignationRepository>();
        services.AddScoped<INumberSequenceRepository, NumberSequenceRepository>();

        // Managers
        services.AddScoped<IHospitalProfileManager, HospitalProfileManager>();
        services.AddScoped<ISystemSettingsManager, SystemSettingsManager>();
        services.AddScoped<IDepartmentManager, DepartmentManager>();
        services.AddScoped<IDesignationManager, DesignationManager>();
        services.AddScoped<INumberSequenceManager, NumberSequenceManager>();

        // Contracts for other modules
        services.AddScoped<AdministrationLookups>();
        services.AddScoped<IDepartmentLookup>(sp => sp.GetRequiredService<AdministrationLookups>());
        services.AddScoped<IDesignationLookup>(sp => sp.GetRequiredService<AdministrationLookups>());
        services.AddScoped<IHospitalSettingsProvider>(sp => sp.GetRequiredService<AdministrationLookups>());
        services.AddScoped<INumberSequenceService, NumberSequenceService>();

        services.AddSingleton<IPermissionDefinitionProvider, AdministrationPermissionDefinitionProvider>();
        services.AddSingleton<INumberSequenceDefinitionProvider, AdministrationNumberSequences>();
        services.AddScoped<IStartupTask, AdministrationSeedTask>();

        return services;
    }
}
