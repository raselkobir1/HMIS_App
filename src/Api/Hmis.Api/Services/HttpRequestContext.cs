using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Presentation.Http;

namespace Hmis.Api.Services;

public sealed class HttpRequestContext(IHttpContextAccessor httpContextAccessor) : IRequestContext
{
    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public string TraceId => HttpContext is null ? string.Empty : TraceIdResolver.Resolve(HttpContext);

    public string? IpAddress => HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => HttpContext?.Request.Headers.UserAgent.ToString();
}
