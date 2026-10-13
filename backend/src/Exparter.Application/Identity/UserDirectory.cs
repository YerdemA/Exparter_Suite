using Exparter.Domain.Identity;

namespace Exparter.Application.Identity;

/// <param name="CanInteract">
/// Whether the account is active and its email verified. Other modules combine this
/// with their own conditions (such as a complete profile) before showing the user to
/// others or letting them act.
/// </param>
public sealed record UserInfo(Guid Id, string Username, bool CanInteract);

/// <summary>What other modules may know about a user account.</summary>
public interface IUserDirectory
{
    Task<UserInfo?> FindAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class UserDirectory(IUserRepository users) : IUserDirectory
{
    public async Task<UserInfo?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? null
            : new UserInfo(user.Id, user.Username, user.Status == UserStatus.Active && user.IsEmailVerified);
    }
}

/// <summary>
/// Published while a registration is being saved, so that handlers in other modules
/// take part in the same transaction.
/// </summary>
public sealed record UserRegistered(Guid UserId) : Mediator.INotification;
