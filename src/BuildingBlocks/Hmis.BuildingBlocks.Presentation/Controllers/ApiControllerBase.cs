using Asp.Versioning;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.BuildingBlocks.Presentation.Controllers;

/// <summary>
/// Base for all module controllers. Success responses only — failures are raised as exceptions
/// by managers and translated by the global exception handler.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult OkResponse<T>(T data, string message = "Request completed successfully.") =>
        Ok(WithTrace(ApiResponse<T>.Ok(data, message)));

    protected IActionResult OkResponse(string message) =>
        Ok(WithTrace(ApiResponse.Ok(message)));

    protected IActionResult CreatedResponse<T>(string actionName, object routeValues, T data, string message = "Resource created successfully.") =>
        CreatedAtAction(actionName, routeValues, WithTrace(ApiResponse<T>.Ok(data, message)));

    private ApiResponse<T> WithTrace<T>(ApiResponse<T> response)
    {
        response.TraceId = TraceIdResolver.Resolve(HttpContext);
        return response;
    }
}
