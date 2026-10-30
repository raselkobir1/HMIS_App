namespace Hmis.BuildingBlocks.Application.Responses;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string BusinessRule = "BUSINESS_RULE_VIOLATION";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string InternalError = "INTERNAL_ERROR";
}
