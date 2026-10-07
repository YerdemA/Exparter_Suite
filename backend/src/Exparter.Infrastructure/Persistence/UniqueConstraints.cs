using Exparter.Application.Common;

namespace Exparter.Infrastructure.Persistence;

internal static class UniqueConstraints
{
    public const string UserEmail = "ix_users_email";
    public const string UserUsername = "ix_users_username";

    public static AppException? ToAppException(string? constraintName) => constraintName switch
    {
        UserEmail => AppException.Conflict("email_already_registered", "The email address is already registered."),
        UserUsername => AppException.Conflict("username_already_taken", "The username is already taken."),
        _ => null,
    };
}
