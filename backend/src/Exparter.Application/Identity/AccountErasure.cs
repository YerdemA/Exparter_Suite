using Exparter.Application.Common;
using Exparter.Domain.Identity;
using Mediator;

namespace Exparter.Application.Identity;

/// <summary>
/// Published while a deletion request is being saved, so that other modules close what
/// the user leaves behind (requests, matches, conversations) in the same transaction.
/// </summary>
public sealed record AccountDeletionRequested(Guid UserId) : INotification;

/// <summary>
/// Published when the time to change one's mind has run out, just before the account
/// itself is erased: each module removes what it holds about the user.
/// </summary>
public sealed record AccountErased(Guid UserId) : INotification;

public interface IBlockedEmailRepository
{
    /// <summary>Whether the address belonged to an account erased too recently to be registered again.</summary>
    Task<bool> IsBlockedAsync(string emailHash, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(BlockedEmail blockedEmail);

    Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Erases the accounts whose deletion can no longer be taken back.</summary>
public sealed record EraseDeletedAccountsCommand : ICommand<int>;

public sealed class EraseDeletedAccountsCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IOneTimeTokenRepository oneTimeTokens,
    IBlockedEmailRepository blockedEmails,
    IPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<EraseDeletedAccountsCommand, int>
{
    /// <summary>Per run; whatever is left over is picked up by the next run.</summary>
    private const int BatchSize = 50;

    /// <returns>How many accounts were erased.</returns>
    public async ValueTask<int> Handle(EraseDeletedAccountsCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await blockedEmails.DeleteExpiredAsync(now, cancellationToken);

        // Each account is saved on its own. Everything done for it can be repeated: if a
        // run stops halfway, the account is still due and the next run finishes the job.
        var due = await users.ListDueForErasureAsync(now, BatchSize, cancellationToken);
        foreach (var user in due)
        {
            await publisher.Publish(new AccountErased(user.Id), cancellationToken);
            await refreshTokens.DeleteByUserAsync(user.Id, cancellationToken);
            await oneTimeTokens.DeleteByUserAsync(user.Id, cancellationToken);
            blockedEmails.Add(BlockedEmail.For(user.Email, now));
            user.Erase(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }
}
