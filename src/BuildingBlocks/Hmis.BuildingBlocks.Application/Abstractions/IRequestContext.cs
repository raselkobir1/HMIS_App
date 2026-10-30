namespace Hmis.BuildingBlocks.Application.Abstractions;

/// <summary>Request metadata for auditing and logging. Single facility — no branch context (decision D3).</summary>
public interface IRequestContext
{
    string TraceId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
