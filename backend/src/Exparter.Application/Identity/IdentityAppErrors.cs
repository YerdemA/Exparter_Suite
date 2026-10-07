using Exparter.Application.Common;

namespace Exparter.Application.Identity;

internal static class IdentityAppErrors
{
    public static AppException EmailAlreadyRegistered() =>
        AppException.Conflict("email_already_registered", "The email address is already registered.");

    public static AppException UsernameAlreadyTaken() =>
        AppException.Conflict("username_already_taken", "The username is already taken.");

    // The same answer for an unknown identifier and a wrong password, so the response
    // does not reveal which accounts exist.
    public static AppException InvalidCredentials() =>
        AppException.Unauthorized("invalid_credentials", "The identifier or password is incorrect.");

    public static AppException AccountSuspended() =>
        AppException.Forbidden("account_suspended", "The account is suspended.");

    public static AppException RefreshTokenInvalid() =>
        AppException.Unauthorized("refresh_token_invalid", "The refresh token is invalid, revoked or expired.");

    public static AppException InvalidToken() =>
        AppException.BadRequest("invalid_token", "The token is invalid, already used or expired.");

    public static AppException InvalidPassword() =>
        AppException.BadRequest("invalid_password", "The password is incorrect.");

    public static AppException AccountNotFound() =>
        AppException.NotFound("The account does not exist.");
}
