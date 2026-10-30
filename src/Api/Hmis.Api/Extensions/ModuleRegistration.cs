using System.Reflection;

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
        // e.g. typeof(Hmis.Modules.Identity.Presentation.IdentityPresentationMarker).Assembly
    ];

    /// <summary>Application assemblies containing FluentValidation validators and Mapster IRegister configs.</summary>
    public static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(Hmis.BuildingBlocks.Application.ApplicationAssemblyMarker).Assembly
    ];

    public static IServiceCollection AddHmisModules(this IServiceCollection services, IConfiguration configuration)
    {
        // services.AddIdentityModule(configuration);
        return services;
    }
}
