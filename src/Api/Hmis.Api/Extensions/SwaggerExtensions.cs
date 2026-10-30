using Microsoft.OpenApi;

namespace Hmis.Api.Extensions;

public static class SwaggerExtensions
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddHmisSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "HMIS API",
                Version = "v1",
                Description = "Hospital Management Information System — REST API"
            });

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the access token returned by POST /api/v1/auth/login."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = []
            });

            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));
        });

        return services;
    }

    public static IApplicationBuilder UseHmisSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "HMIS API v1");
            options.DocumentTitle = "HMIS API";
        });
        return app;
    }
}
