using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.Modules.Identity.Application.Interfaces.Managers;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Interfaces.Services;
using Hmis.Modules.Identity.Application.Managers;
using Hmis.Modules.Identity.Application.Options;
using Hmis.Modules.Identity.Application.Security;
using Hmis.Modules.Identity.Application.Services;
using Hmis.Modules.Identity.Infrastructure.Persistence;
using Hmis.Modules.Identity.Infrastructure.Repositories;
using Hmis.Modules.Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.SchemaName);
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AccountSecurityOptions>(configuration.GetSection(AccountSecurityOptions.SectionName));
        services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));
        services.Configure<Hmis.BuildingBlocks.Application.Seeding.DemoDataOptions>(
            configuration.GetSection(Hmis.BuildingBlocks.Application.Seeding.DemoDataOptions.SectionName));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ILoginHistoryRepository, LoginHistoryRepository>();

        // Services
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<SuperAdministratorGuard>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<Hmis.BuildingBlocks.Contracts.Identity.IUserEmployeeLookup, UserEmployeeLookup>();
        services.AddScoped<Hmis.BuildingBlocks.Contracts.Identity.IUserDirectory, UserDirectory>();
        services.AddSingleton<IPermissionDefinitionProvider, IdentityPermissionDefinitionProvider>();
        services.AddScoped<IStartupTask, IdentitySeedTask>();
        services.AddScoped<IStartupTask, IdentityDemoUsersSeedTask>();

        // Managers
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IUserManager, UserManager>();
        services.AddScoped<IRoleManager, RoleManager>();
        services.AddScoped<IPermissionManager, PermissionManager>();
        services.AddScoped<ILoginHistoryManager, LoginHistoryManager>();

        return services;
    }
}
