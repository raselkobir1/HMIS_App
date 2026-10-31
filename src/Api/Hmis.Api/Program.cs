using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using Hmis.Api.Extensions;
using Hmis.Api.Filters;
using Hmis.Api.HealthChecks;
using Hmis.Api.Middleware;
using Hmis.Api.Services;
using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Presentation;
using Hmis.BuildingBlocks.Presentation.Http;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName());

    // ---------- Cross-cutting services ----------
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
    builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
    builder.Services.AddBuildingBlocksInfrastructure(builder.Configuration);

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // ---------- Validation & mapping ----------
    builder.Services.AddValidatorsFromAssemblies(ModuleRegistration.ApplicationAssemblies, includeInternalTypes: true);
    var mapsterConfig = TypeAdapterConfig.GlobalSettings;
    mapsterConfig.Scan(ModuleRegistration.ApplicationAssemblies);
    builder.Services.AddSingleton(mapsterConfig);
    builder.Services.AddScoped<IMapper, ServiceMapper>();

    // ---------- Security ----------
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddPermissionAuthorization();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    var authPermitPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(RateLimitPolicies.Auth, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitPerMinute, Window = TimeSpan.FromMinutes(1) }));
        options.OnRejected = (context, cancellationToken) => new ValueTask(
            context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Fail(
                "Too many requests. Please try again later.",
                [new ApiError(ErrorCodes.TooManyRequests, "Too many requests. Please try again later.")],
                TraceIdResolver.Resolve(context.HttpContext)), cancellationToken));
    });

    // ---------- Modules ----------
    builder.Services.AddHmisModules(builder.Configuration);
    builder.Services.TryAddScoped<IPermissionChecker, DenyAllPermissionChecker>();

    // ---------- MVC ----------
    var mvc = builder.Services
        .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            // Never echo JSON parser exception text (type names, positions) back to clients.
            options.AllowInputFormatterExceptionMessages = false;
        });
    foreach (var assembly in ModuleRegistration.PresentationAssemblies)
    {
        mvc.AddApplicationPart(assembly);
    }

    // Validation errors are produced by FluentValidationFilter in the standard ApiResponse format.
    builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddHmisSwagger();
    builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");

    // ---------- Pipeline ----------
    var app = builder.Build();
    await app.RunStartupTasksAsync();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnostics, httpContext) =>
            diagnostics.Set("TraceId", TraceIdResolver.Resolve(httpContext)));

    if (app.Environment.IsDevelopment())
    {
        app.UseHmisSwagger();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "HMIS API terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
