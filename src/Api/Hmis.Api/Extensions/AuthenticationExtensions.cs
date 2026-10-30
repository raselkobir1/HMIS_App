using System.Text;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Hmis.Api.Extensions;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Secret: supplied by environment variable Jwt__SigningKey or user-secrets, never appsettings.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

        if (string.IsNullOrWhiteSpace(settings.SigningKey) || Encoding.UTF8.GetByteCount(settings.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or shorter than 32 bytes. Set it via user-secrets or the Jwt__SigningKey environment variable.");
        }

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role"
                };

                // Return the standard ApiResponse envelope instead of empty 401/403 bodies.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteErrorAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                            ErrorCodes.Unauthorized, "Authentication is required.");
                    },
                    OnForbidden = context => WriteErrorAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                        ErrorCodes.Forbidden, "You do not have permission to perform this action.")
                };
            });

        return services;
    }

    private static Task WriteErrorAsync(HttpContext httpContext, int statusCode, string code, string message)
    {
        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(
            ApiResponse.Fail(message, [new ApiError(code, message)], TraceIdResolver.Resolve(httpContext)));
    }
}
