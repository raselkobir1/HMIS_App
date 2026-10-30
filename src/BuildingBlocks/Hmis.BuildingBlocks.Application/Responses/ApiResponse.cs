namespace Hmis.BuildingBlocks.Application.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<ApiError> Errors { get; set; } = [];
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Request completed successfully.") =>
        new() { Success = true, Message = message, Data = data };
}

/// <summary>Response without a data payload (used for failures and data-less successes).</summary>
public sealed class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message) => new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, IEnumerable<ApiError> errors, string? traceId) =>
        new() { Success = false, Message = message, Errors = errors.ToList(), TraceId = traceId };
}
