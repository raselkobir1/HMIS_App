using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Auditing;
using Hmis.Modules.Audit.Application;
using Hmis.Modules.Audit.Infrastructure.Persistence;
using Hmis.Modules.Audit.Infrastructure.Repositories;
using Hmis.Modules.Audit.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Audit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AuditDbContext>(configuration, AuditDbContext.SchemaName);

        services.AddSingleton<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogManager, AuditLogManager>();
        services.AddSingleton<IPermissionDefinitionProvider, AuditPermissionDefinitionProvider>();

        return services;
    }
}
