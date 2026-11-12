using Exparter.Application.Common;
using Exparter.Application.Matching;
using Exparter.Application.Sessions;
using Exparter.Application.Tests.Identity;
using Exparter.Domain.Common;
using Exparter.Domain.Sessions;

namespace Exparter.Application.Tests.Sessions;

internal sealed class FakeCalls : ICallTokenIssuer, ICallRooms
{
    public HashSet<string> Rooms { get; } = [];

    public List<(string Room, Guid UserId, DateTimeOffset ExpiresAt)> Issued { get; } = [];

    public CallTokenDto Issue(string room, Guid userId, DateTimeOffset expiresAt)
    {
        Issued.Add((room, userId, expiresAt));
        return new CallTokenDto("wss://rtc.test", $"token-{room}-{userId}", expiresAt);
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Rooms.ToList());

    public Task CloseAsync(string room, CancellationToken cancellationToken)
    {
        Rooms.Remove(room);
        return Task.CompletedTask;
    }
}

public class CallHandlerTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly InMemorySessions _sessions = new();
    private readonly FakeMatches _matches = new();
    private readonly FakeCalls _calls = new();
    private readonly TestClock _clock = new();

    /// <summary>An accepted online session of an hour that starts in a day, in a match of its own.</summary>
    private SwapSession Scheduled()
    {
        var matchId = Guid.NewGuid();
        SetMatch(matchId, isActive: true);
        var session = SwapSession.Propose(matchId, _ali, _ayse, _clock.GetUtcNow().AddDays(1), 60, null, _clock.GetUtcNow());
        session.Accept(_ayse, _clock.GetUtcNow());
        _sessions.Add(session);
        return session;
    }

    private void SetMatch(Guid matchId, bool isActive, bool hasPendingEndRequest = false) =>
        _matches.Items[matchId] = new MatchInfo(matchId, _ali, _ayse, isActive, hasPendingEndRequest);

    /// <summary>The session marked completed during its call, and then the match ended.</summary>
    private SwapSession CompletedInEndedMatch()
    {
        var session = Scheduled();
        _clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(30));
        session.MarkCompleted(_ali, _clock.GetUtcNow());
        session.CancelBecauseMatchEnded(_clock.GetUtcNow());
        SetMatch(session.MatchId, isActive: false);
        return session;
    }

    private Task<CallTokenDto> JoinAsync(Guid sessionId, Guid userId) =>
        new CreateCallTokenCommandHandler(_sessions, _matches, _calls, _clock)
            .Handle(new CreateCallTokenCommand(userId, sessionId), CancellationToken.None).AsTask();

    private Task<int> CloseEndedAsync() =>
        new CloseEndedCallsCommandHandler(_calls, _sessions, _matches, _clock)
            .Handle(new CloseEndedCallsCommand(), CancellationToken.None).AsTask();

    [Fact]
    public async Task Both_users_get_a_token_for_the_session_room_that_lasts_until_the_call_closes()
    {
        var session = Scheduled();
        _clock.Advance(TimeSpan.FromDays(1) - TimeSpan.FromMinutes(5));

        var token = await JoinAsync(session.Id, _ali);
        await JoinAsync(session.Id, _ayse);

        Assert.Equal(("wss://rtc.test", session.CallClosesAt), (token.Url, token.ExpiresAt));
        Assert.Equal(
            [(session.Id.ToString(), _ali, session.CallClosesAt), (session.Id.ToString(), _ayse, session.CallClosesAt)],
            _calls.Issued);
    }

    [Fact]
    public async Task No_token_is_given_outside_the_window_or_to_anyone_else()
    {
        var session = Scheduled();

        Assert.Equal(SessionErrors.CallNotOpenYetCode, (await Assert.ThrowsAsync<DomainException>(() => JoinAsync(session.Id, _ali))).Code);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<AppException>(() => JoinAsync(session.Id, Guid.NewGuid()))).Code);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<AppException>(() => JoinAsync(Guid.NewGuid(), _ali))).Code);

        _clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(75));
        Assert.Equal(SessionErrors.CallWindowClosedCode, (await Assert.ThrowsAsync<DomainException>(() => JoinAsync(session.Id, _ali))).Code);
        Assert.Empty(_calls.Issued);
    }

    [Fact]
    public async Task Rooms_whose_call_ended_are_closed_and_open_ones_are_left_alone()
    {
        var open = Scheduled();
        var cancelled = Scheduled();
        var later = SwapSession.Propose(Guid.NewGuid(), _ali, _ayse, _clock.GetUtcNow().AddDays(3), 60, null, _clock.GetUtcNow());
        later.Accept(_ayse, _clock.GetUtcNow());
        _sessions.Add(later);
        _clock.Advance(TimeSpan.FromDays(1));
        cancelled.Cancel(_ali, "Something came up", _clock.GetUtcNow());
        _calls.Rooms.UnionWith([open.Id.ToString(), cancelled.Id.ToString(), later.Id.ToString(), Guid.NewGuid().ToString(), "not-a-session"]);

        Assert.Equal(4, await CloseEndedAsync());
        Assert.Equal([open.Id.ToString()], _calls.Rooms);

        _clock.Advance(TimeSpan.FromMinutes(75));
        Assert.Equal(1, await CloseEndedAsync());
        Assert.Empty(_calls.Rooms);
    }

    [Fact]
    public async Task A_session_marked_completed_has_no_call_once_its_match_ended()
    {
        var session = CompletedInEndedMatch();

        var error = await Assert.ThrowsAsync<AppException>(() => JoinAsync(session.Id, _ali));

        Assert.Equal(CreateCallTokenCommandHandler.CallMatchEndedCode, error.Code);
        Assert.Empty(_calls.Issued);
    }

    [Fact]
    public async Task The_call_goes_on_while_a_request_to_end_the_match_is_pending()
    {
        var session = Scheduled();
        _clock.Advance(TimeSpan.FromDays(1));
        SetMatch(session.MatchId, isActive: false, hasPendingEndRequest: true);
        _calls.Rooms.Add(session.Id.ToString());

        await JoinAsync(session.Id, _ali);

        Assert.Equal(0, await CloseEndedAsync());
    }

    [Fact]
    public async Task The_room_of_a_session_whose_match_ended_is_closed_within_its_window()
    {
        var session = CompletedInEndedMatch();
        _calls.Rooms.Add(session.Id.ToString());

        Assert.Equal(1, await CloseEndedAsync());
        Assert.Empty(_calls.Rooms);
    }
}
