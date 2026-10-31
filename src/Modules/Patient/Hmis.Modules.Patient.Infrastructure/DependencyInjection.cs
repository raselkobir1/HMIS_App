using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.Modules.Patient.Application;
using Hmis.Modules.Patient.Infrastructure.Persistence;
using Hmis.Modules.Patient.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Patient.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPatientModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PatientDbContext>(configuration, PatientDbContext.SchemaName);
        services.AddScoped<IPatientUnitOfWork, PatientUnitOfWork>();

        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPatientAllergyRepository, PatientAllergyRepository>();
        services.AddScoped<IPatientMedicalHistoryRepository, PatientMedicalHistoryRepository>();
        services.AddScoped<IPatientDocumentRepository, PatientDocumentRepository>();
        services.AddScoped<IPatientMergeRequestRepository, PatientMergeRequestRepository>();

        services.AddScoped<PatientManager>();
        services.AddScoped<IPatientManager>(sp => sp.GetRequiredService<PatientManager>());
        services.AddScoped<IPatientLookup>(sp => sp.GetRequiredService<PatientManager>());
        services.AddScoped<IPatientMedicalManager, PatientMedicalManager>();
        services.AddScoped<IPatientDocumentManager, PatientDocumentManager>();
        services.AddScoped<IPatientMergeManager, PatientMergeManager>();

        services.AddSingleton<IPermissionDefinitionProvider, PatientPermissionDefinitionProvider>();
        return services;
    }
}
