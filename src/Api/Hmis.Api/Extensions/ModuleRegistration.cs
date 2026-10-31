using System.Reflection;
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
        typeof(Hmis.Modules.Identity.Presentation.IdentityPresentationMarker).Assembly
    ];

    /// <summary>Application assemblies containing FluentValidation validators and Mapster IRegister configs.</summary>
    public static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(Hmis.BuildingBlocks.Application.ApplicationAssemblyMarker).Assembly,
        typeof(Hmis.Modules.Identity.Application.IdentityApplicationMarker).Assembly
    ];

    public static IServiceCollection AddHmisModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityModule(configuration);
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
