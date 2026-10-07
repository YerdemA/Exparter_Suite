using Exparter.Domain.Common;

namespace Exparter.Domain.Identity;

public static class IdentityErrors
{
    public const string AgeNotConfirmedCode = "age_not_confirmed";
    public const string InvalidUsernameCode = "invalid_username";
    public const string UsernameReservedCode = "username_reserved";
    public const string EmailAlreadyVerifiedCode = "email_already_verified";
    public const string InvalidTokenCode = "invalid_token";
    public const string InvalidStateCode = "invalid_state";

    public static DomainException AgeNotConfirmed() =>
        new(AgeNotConfirmedCode, "The user must confirm being 18 or older.");

    public static DomainException InvalidUsername() =>
        new(InvalidUsernameCode, "The username has an invalid format.");

    public static DomainException UsernameReserved() =>
        new(UsernameReservedCode, "The username is reserved.");

    public static DomainException EmailAlreadyVerified() =>
        new(EmailAlreadyVerifiedCode, "The email address is already verified.");

    public static DomainException InvalidToken() =>
        new(InvalidTokenCode, "The token is invalid, already used or expired.");

    public static DomainException InvalidState(string message) =>
        new(InvalidStateCode, message);
}
