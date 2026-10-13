using Exparter.Application.Identity;
using Exparter.Application.Profiles;

namespace Exparter.Application.Common;

/// <summary>
/// Builds the short description of a user that other resources embed (a swap request's
/// requester, a match's two sides). It belongs to no module: it combines what Identity
/// and Profiles each know about the user.
/// </summary>
public interface IUserSummaries
{
    /// <returns>One entry per requested id; an unknown id yields a "deleted user" summary.</returns>
    Task<IReadOnlyDictionary<Guid, UserSummaryDto>> GetAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

internal sealed class UserSummaries(IUserDirectory users, IProfileDirectory profiles, IObjectStorage storage) : IUserSummaries
{
    private static readonly TimeSpan AvatarUrlLifetime = TimeSpan.FromHours(1);

    public async Task<IReadOnlyDictionary<Guid, UserSummaryDto>> GetAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var distinct = userIds.Distinct().ToList();
        var accounts = (await users.FindManyAsync(distinct, cancellationToken)).ToDictionary(user => user.Id);
        var cards = (await profiles.GetCardsAsync(distinct, cancellationToken)).ToDictionary(card => card.UserId);

        return distinct.ToDictionary(
            id => id,
            id =>
            {
                // An erased account is described like one that was never there.
                if (!accounts.TryGetValue(id, out var account) || account.IsDeleted)
                {
                    return new UserSummaryDto(id, null, null, null, IsDeleted: true);
                }

                var card = cards.GetValueOrDefault(id);
                return new UserSummaryDto(
                    id,
                    account.Username,
                    card?.DisplayName,
                    card?.AvatarKey is null ? null : storage.CreateDownloadUrl(card.AvatarKey, AvatarUrlLifetime),
                    IsDeleted: false);
            });
    }
}
