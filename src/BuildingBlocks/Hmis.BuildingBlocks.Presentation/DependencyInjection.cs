using Hmis.BuildingBlocks.Presentation.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.BuildingBlocks.Presentation;

public static class DependencyInjection
{
    /// <summary>Registers permission-based authorization. IPermissionChecker is provided by the Identity module.</summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
