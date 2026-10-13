using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Sessions;

namespace Exparter.Application.Common;

/// <summary>
/// What a user's history of swaps says about them, shown on their profile to help
/// others decide whether to trust them.
/// </summary>
/// <param name="TotalMatches">Every match the user has had, running ones included.</param>
/// <param name="CompletedMatches">Ended matches in which at least one session was completed.</param>
/// <param name="CancelledMatches">Ended matches in which no session was completed.</param>
/// <param name="CompletedSessions">Sessions completed across all matches.</param>
public sealed record UserStatsDto(int TotalMatches, int CompletedMatches, int CancelledMatches, int CompletedSessions);

/// <summary>
/// Derives a user's statistics from the records of matches and sessions. It belongs to
/// no module: it combines what Matching and Sessions each know.
/// </summary>
public interface IUserStats
{
    /// <summary>Throws the not-found error for an unknown or inactive user.</summary>
    Task<UserStatsDto> GetAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class UserStats(IUserDirectory users, IMatchDirectory matches, ISessionDirectory sessions) : IUserStats
{
    public async Task<UserStatsDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await users.FindAsync(userId, cancellationToken) is not { IsActive: true })
        {
            throw AppException.NotFound("The user was not found.");
        }

        var history = await matches.ListForUserAsync(userId, cancellationToken);
        var ended = history.Where(match => match.IsEnded).Select(match => match.Id).ToList();
        var withCompletedSession = await sessions.GetMatchIdsWithCompletedSessionAsync(ended, cancellationToken);
        var completed = ended.Count(withCompletedSession.Contains);
        return new UserStatsDto(
            history.Count,
            completed,
            ended.Count - completed,
            await sessions.CountCompletedAsync(userId, cancellationToken));
    }
}

public sealed record GetUserStatsQuery(Guid UserId) : Mediator.IQuery<UserStatsDto>;

public sealed class GetUserStatsQueryHandler(IUserStats stats) : Mediator.IQueryHandler<GetUserStatsQuery, UserStatsDto>
{
    public async ValueTask<UserStatsDto> Handle(GetUserStatsQuery query, CancellationToken cancellationToken) =>
        await stats.GetAsync(query.UserId, cancellationToken);
}
