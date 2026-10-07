namespace Exparter.Domain.Common;

/// <summary>
/// Thrown when an operation would break a business rule. <see cref="Code"/> is the
/// stable error code exposed to API clients.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
