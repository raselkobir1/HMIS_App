using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Hmis.BuildingBlocks.Presentation.Http;

public static class TraceIdResolver
{
    /// <summary>W3C trace id when available (matches Serilog's TraceId), otherwise the ASP.NET request id.</summary>
    public static string Resolve(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}
