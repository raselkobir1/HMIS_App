using Hmis.BuildingBlocks.Application.Responses;

namespace Hmis.BuildingBlocks.Application.Exceptions;

/// <summary>Base for expected application failures; the global exception handler maps each subtype to an HTTP status.</summary>
public abstract class AppException(string code, string message, IEnumerable<ApiError>? errors = null) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyList<ApiError> Errors { get; } = errors?.ToList() ?? [new ApiError(code, message)];
}

/// <summary>400 — request is invalid (normally raised by the validation filter).</summary>
public sealed class ValidationException(IEnumerable<ApiError> errors)
    : AppException(ErrorCodes.ValidationError, "One or more validation errors occurred.", errors);

/// <summary>401 — caller is not authenticated or credentials are invalid.</summary>
public sealed class UnauthorizedException(string message = "Authentication is required.", string code = ErrorCodes.Unauthorized)
    : AppException(code, message);

/// <summary>403 — caller is authenticated but not allowed.</summary>
public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(ErrorCodes.Forbidden, message);

/// <summary>404 — resource does not exist.</summary>
public sealed class NotFoundException(string entityName, object key)
    : AppException(ErrorCodes.NotFound, $"{entityName} '{key}' was not found.");

/// <summary>409 — conflicts with current state (duplicate identifier, concurrent update).</summary>
public sealed class ConflictException(string message, string code = ErrorCodes.Conflict)
    : AppException(code, message);

/// <summary>422 — request is well-formed but violates a business rule.</summary>
public sealed class BusinessException(string code, string message, string? field = null)
    : AppException(code, message, [new ApiError(code, message, field)]);
