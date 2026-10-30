namespace Hmis.BuildingBlocks.Domain.Exceptions;

/// <summary>Raised when a domain invariant is violated. Mapped to HTTP 422.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
