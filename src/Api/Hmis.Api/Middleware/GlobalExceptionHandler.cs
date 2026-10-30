using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Domain.Exceptions;
using Hmis.BuildingBlocks.Presentation.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hmis.Api.Middleware;

/// <summary>
/// Translates exceptions into the standard ApiResponse envelope with the correct HTTP status.
/// Internal details are never returned to the client; unexpected errors are logged with the trace id.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled by the client.");
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (statusCode, message, errors) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Request failed with {StatusCode}: {ErrorCode}",
                statusCode, errors.FirstOrDefault()?.Code);
        }

        var response = ApiResponse.Fail(message, errors, TraceIdResolver.Resolve(httpContext));
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Message, IReadOnlyList<ApiError> Errors) Map(Exception exception) =>
        exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, e.Message, e.Errors),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, e.Message, e.Errors),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Message, e.Errors),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Message, e.Errors),
            ConflictException e => (StatusCodes.Status409Conflict, e.Message, e.Errors),
            BusinessException e => (StatusCodes.Status422UnprocessableEntity, e.Message, e.Errors),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Message, [new ApiError(e.Code, e.Message)]),

            DbUpdateConcurrencyException => Conflict(ErrorCodes.ConcurrencyConflict,
                "The record was modified by another user. Reload and try again."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                Conflict(ErrorCodes.Conflict, "A record with the same unique value already exists."),

            BadHttpRequestException e => (e.StatusCode, "The request could not be processed.",
                [new ApiError(ErrorCodes.ValidationError, "The request could not be processed.")]),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.",
                [new ApiError(ErrorCodes.InternalError, "An unexpected error occurred. Use the trace id when reporting this issue.")])
        };

    private static (int, string, IReadOnlyList<ApiError>) Conflict(string code, string message) =>
        (StatusCodes.Status409Conflict, message, [new ApiError(code, message)]);
}
