namespace Exparter.Application.Common;

public enum AppErrorKind
{
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

/// <summary>
/// An expected failure of a use case. The API layer turns <see cref="Kind"/> into the
/// HTTP status and <see cref="Code"/> into the problem details error code.
/// </summary>
public sealed class AppException(AppErrorKind kind, string code, string message) : Exception(message)
{
    public AppErrorKind Kind { get; } = kind;

    public string Code { get; } = code;

    public static AppException BadRequest(string code, string message) => new(AppErrorKind.BadRequest, code, message);

    public static AppException Unauthorized(string code, string message) => new(AppErrorKind.Unauthorized, code, message);

    public static AppException Forbidden(string code, string message) => new(AppErrorKind.Forbidden, code, message);

    public static AppException NotFound(string message) => new(AppErrorKind.NotFound, "not_found", message);

    public static AppException Conflict(string code, string message) => new(AppErrorKind.Conflict, code, message);
}
