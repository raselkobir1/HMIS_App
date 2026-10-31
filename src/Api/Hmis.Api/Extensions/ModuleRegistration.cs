using System.Reflection;
using Hmis.Modules.Administration.Infrastructure;
using Hmis.Modules.Audit.Infrastructure;
using Hmis.Modules.HR.Infrastructure;
using Hmis.Modules.Patient.Infrastructure;
using Hmis.Modules.Identity.Infrastructure;

namespace Hmis.Api.Extensions;

/// <summary>
/// Single place where business modules are plugged into the host.
/// Each approved module adds: services.AddXModule(configuration) and its Presentation assembly below.
/// </summary>
public static class ModuleRegistration
{
    /// <summary>Presentation assemblies whose controllers are exposed by the API.</summary>
    public static readonly Assembly[] PresentationAssemblies =
    [
        typeof(Hmis.Modules.Identity.Presentation.IdentityPresentationMarker).Assembly,
        typeof(Hmis.Modules.Audit.Presentation.AuditPresentationMarker).Assembly,
        typeof(Hmis.Modules.Administration.Presentation.Controllers.AdministrationPresentationMarker).Assembly,
        typeof(Hmis.Modules.HR.Presentation.HrPresentationMarker).Assembly,
        typeof(Hmis.Modules.Patient.Presentation.PatientPresentationMarker).Assembly
    ];

    /// <summary>Application assemblies containing FluentValidation validators and Mapster IRegister configs.</summary>
    public static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(Hmis.BuildingBlocks.Application.ApplicationAssemblyMarker).Assembly,
        typeof(Hmis.Modules.Identity.Application.IdentityApplicationMarker).Assembly,
        typeof(Hmis.Modules.Audit.Application.AuditApplicationMarker).Assembly,
        typeof(Hmis.Modules.Administration.Application.AdministrationApplicationMarker).Assembly,
        typeof(Hmis.Modules.HR.Application.HrApplicationMarker).Assembly,
        typeof(Hmis.Modules.Patient.Application.PatientApplicationMarker).Assembly
    ];

    public static IServiceCollection AddHmisModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuditModule(configuration);
        services.AddIdentityModule(configuration);
        services.AddAdministrationModule(configuration);
        services.AddHrModule(configuration);
        services.AddPatientModule(configuration);
        return services;
    }

    /// <summary>Runs every module's IStartupTask (seeding) in order. Requires the database to be migrated.</summary>
    public static async Task RunStartupTasksAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var tasks = scope.ServiceProvider.GetServices<Hmis.BuildingBlocks.Application.Abstractions.IStartupTask>()
            .OrderBy(t => t.Order);

        foreach (var task in tasks)
        {
            try
            {
                await task.ExecuteAsync(app.Lifetime.ApplicationStopping);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Startup task {task.GetType().Name} failed. Has the database been migrated? Run scripts/migrate.ps1.", ex);
            }
        }
    }
}
