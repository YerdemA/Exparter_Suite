namespace Exparter.Domain.Common;

/// <summary>Error codes that mean the same thing in every module.</summary>
public static class DomainErrorCodes
{
    /// <summary>
    /// The user is not the one who may take this action (for example the sender of a
    /// request trying to accept it). The API answers 403 instead of the usual 409.
    /// </summary>
    public const string NotParticipant = "not_participant";
}
