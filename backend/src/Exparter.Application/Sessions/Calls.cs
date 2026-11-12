using Exparter.Application.Common;
using Exparter.Application.Matching;
using Mediator;

namespace Exparter.Application.Sessions;

public sealed record CallTokenDto(string Url, string Token, DateTimeOffset ExpiresAt);

/// <summary>Signs the tokens with which clients join a room on the media server.</summary>
public interface ICallTokenIssuer
{
    CallTokenDto Issue(string room, Guid userId, DateTimeOffset expiresAt);
}

/// <summary>The rooms that are open on the media server: one per session whose call someone joined.</summary>
public interface ICallRooms
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Disconnects everyone in the room. Closing a room that is already gone is not an error.</summary>
    Task CloseAsync(string room, CancellationToken cancellationToken);
}

public sealed record CreateCallTokenCommand(Guid UserId, Guid SessionId) : ICommand<CallTokenDto>;

public sealed class CreateCallTokenCommandHandler(
    ISessionRepository sessions,
    IMatchDirectory matches,
    ICallTokenIssuer issuer,
    TimeProvider timeProvider) : ICommandHandler<CreateCallTokenCommand, CallTokenDto>
{
    public async ValueTask<CallTokenDto> Handle(CreateCallTokenCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.GetForParticipantAsync(command.SessionId, command.UserId, cancellationToken);
        session.EnsureCanJoinCall(command.UserId, timeProvider.GetUtcNow());

        // Ending the match cancels only the sessions still ahead; one marked completed
        // keeps its window, so the match is what closes its call.
        if (!await MatchIsRunningAsync(matches, session.MatchId, cancellationToken))
        {
            throw AppException.Conflict(CallMatchEndedCode, "The match of this session has ended, so its call cannot be joined.");
        }

        return issuer.Issue(session.Id.ToString(), command.UserId, session.CallClosesAt);
    }

    public const string CallMatchEndedCode = "call_match_ended";

    internal static async Task<bool> MatchIsRunningAsync(IMatchDirectory matches, Guid matchId, CancellationToken cancellationToken) =>
        await matches.FindAsync(matchId, cancellationToken) is { IsEnded: false };
}

/// <summary>
/// Closes the rooms whose call should no longer be going on: the window closed, the
/// session was cancelled, or its match ended. The media server checks a token only when
/// someone joins, so without this a call could go on past its window.
/// </summary>
public sealed record CloseEndedCallsCommand : ICommand<int>;

public sealed class CloseEndedCallsCommandHandler(
    ICallRooms rooms,
    ISessionRepository sessions,
    IMatchDirectory matches,
    TimeProvider timeProvider) : ICommandHandler<CloseEndedCallsCommand, int>
{
    /// <returns>How many rooms were closed.</returns>
    public async ValueTask<int> Handle(CloseEndedCallsCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var closed = 0;
        foreach (var room in await rooms.ListAsync(cancellationToken))
        {
            if (Guid.TryParse(room, out var sessionId)
                && await sessions.GetByIdAsync(sessionId, cancellationToken) is { } session
                && session.IsCallOpenAt(now)
                && await CreateCallTokenCommandHandler.MatchIsRunningAsync(matches, session.MatchId, cancellationToken))
            {
                continue;
            }

            await rooms.CloseAsync(room, cancellationToken);
            closed++;
        }

        return closed;
    }
}
