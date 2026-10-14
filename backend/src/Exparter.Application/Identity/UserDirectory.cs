using Exparter.Domain.Identity;

namespace Exparter.Application.Identity;

/// <param name="IsActive">False for suspended accounts and accounts pending deletion.</param>
public sealed record UserInfo(Guid Id, string Username, bool IsActive, bool IsEmailVerified, bool IsDeleted = false);

/// <summary>What other modules may know about a user account.</summary>
public interface IUserDirectory
{
    Task<UserInfo?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The accounts that exist among the given ids, in no particular order.</summary>
    Task<IReadOnlyList<UserInfo>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

internal sealed class UserDirectory(IUserRepository users) : IUserDirectory
{
    public async Task<UserInfo?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToInfo(user);
    }

    public async Task<IReadOnlyList<UserInfo>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        (await users.ListByIdsAsync(userIds, cancellationToken)).Select(ToInfo).ToList();

    private static UserInfo ToInfo(User user) =>
        new(user.Id, user.Username, user.Status == UserStatus.Active, user.IsEmailVerified, user.Status == UserStatus.Deleted);
}

/// <summary>
/// Published while a registration is being saved, so that handlers in other modules
/// take part in the same transaction.
/// </summary>
public sealed record UserRegistered(Guid UserId) : Mediator.INotification;
