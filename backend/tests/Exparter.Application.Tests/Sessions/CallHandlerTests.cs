using Exparter.Application.Common;
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
    private readonly FakeCalls _calls = new();
    private readonly TestClock _clock = new();

    /// <summary>An accepted online session of an hour that starts in a day.</summary>
    private SwapSession Scheduled()
    {
        var session = SwapSession.ProposeOnline(Guid.NewGuid(), _ali, _ayse, _clock.GetUtcNow().AddDays(1), 60, null, _clock.GetUtcNow());
        session.Accept(_ayse, _clock.GetUtcNow());
        _sessions.Add(session);
        return session;
    }

    private Task<CallTokenDto> JoinAsync(Guid sessionId, Guid userId) =>
        new CreateCallTokenCommandHandler(_sessions, _calls, _clock)
            .Handle(new CreateCallTokenCommand(userId, sessionId), CancellationToken.None).AsTask();

    private Task<int> CloseEndedAsync() =>
        new CloseEndedCallsCommandHandler(_calls, _sessions, _clock)
            .Handle(new CloseEndedCallsCommand(), CancellationToken.None).AsTask();

    [Fact]
    public async Task Both_users_get_a_token_for_the_session_room_that_lasts_until_the_call_closes()
    {
        var session = Scheduled();
        _clock.Advance(TimeSpan.FromDays(1) - TimeSpan.FromMinutes(5));

        var token = await JoinAsync(session.Id, _ali);
        await JoinAsync(session.Id, _ayse);

        Assert.Equal(("wss://rtc.test", session.CallClosesAt!.Value), (token.Url, token.ExpiresAt));
        Assert.Equal(
            [(session.Id.ToString(), _ali, session.CallClosesAt.Value), (session.Id.ToString(), _ayse, session.CallClosesAt.Value)],
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
        var later = SwapSession.ProposeOnline(Guid.NewGuid(), _ali, _ayse, _clock.GetUtcNow().AddDays(3), 60, null, _clock.GetUtcNow());
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
}
