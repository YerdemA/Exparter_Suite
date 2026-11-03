using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Sessions;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Tests.Matching;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Common;
using Exparter.Domain.Notifications;
using Exparter.Domain.Sessions;

namespace Exparter.Application.Tests.Sessions;

internal sealed class InMemorySessions : ISessionRepository
{
    public List<SwapSession> Items { get; } = [];

    public Task<SwapSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<PagedList<SwapSession>> ListCancelledByUsersAsync(PageRequest page, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(
            Items.Where(x => x.Status == SwapSessionStatus.Cancelled && x.CancelReason is not null).OrderByDescending(x => x.CancelledAt),
            page));

    public Task<PagedList<SwapSession>> ListByMatchAsync(Guid matchId, PageRequest page, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(Items.Where(x => x.MatchId == matchId), page));

    public Task<PagedList<SwapSession>> ListForUserAsync(
        Guid userId,
        SwapSessionStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset now,
        PageRequest page,
        CancellationToken cancellationToken) =>
        Task.FromResult(Paged(
            Items.Where(x => x.IsParticipant(userId)
                && (status is null || x.StatusAt(now) == status)
                && (from is null || x.ScheduledAt >= from)
                && (to is null || x.ScheduledAt < to)),
            page));

    public Task<IReadOnlyList<SwapSession>> ListUpcomingByMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwapSession>>(Items
            .Where(x => x.MatchId == matchId && x.Status is SwapSessionStatus.Proposed or SwapSessionStatus.Scheduled)
            .ToList());

    public Task<IReadOnlyList<SwapSession>> ListOverdueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwapSession>>(Items.Where(x => x.Status != x.StatusAt(now)).Take(limit).ToList());

    public Task<IReadOnlyList<SwapSession>> ListDueForReminderAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwapSession>>(Items
            .Where(x => x.Status is SwapSessionStatus.Scheduled or SwapSessionStatus.CompletionPending)
            .Take(limit)
            .ToList());

    public Task<int> CountCompletedForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(x => x.IsParticipant(userId) && x.StatusAt(now) == SwapSessionStatus.Completed));

    public Task<IReadOnlySet<Guid>> ListMatchIdsWithCompletedSessionAsync(
        IReadOnlyCollection<Guid> matchIds,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(Items
            .Where(x => matchIds.Contains(x.MatchId) && x.StatusAt(now) == SwapSessionStatus.Completed)
            .Select(x => x.MatchId)
            .ToHashSet());

    public Task<bool> HasInPersonMeetingBetweenAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.Mode == SessionMode.InPerson
            && x.Status != SwapSessionStatus.Cancelled
            && x.IsParticipant(userId)
            && x.IsParticipant(otherUserId)));

    public Task<Guid?> FindCompletedBetweenAsync(Guid userId, Guid otherUserId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(x => x.IsParticipant(userId) && x.IsParticipant(otherUserId) && x.StatusAt(now) == SwapSessionStatus.Completed)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefault());

    public Task<IReadOnlyList<SwapSession>> ListOnlineAroundAsync(
        IReadOnlyCollection<Guid> userIds,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwapSession>>(Items
            .Where(x => x.Mode == SessionMode.Online && userIds.Any(x.IsParticipant) && x.ScheduledAt < end)
            .ToList());

    public void Add(SwapSession session) => Items.Add(session);

    private static PagedList<SwapSession> Paged(IEnumerable<SwapSession> source, PageRequest page)
    {
        var all = source.OrderByDescending(x => x.ScheduledAt ?? x.CreatedAt).ToList();
        return new PagedList<SwapSession>(all, page.Page, page.PageSize, all.Count);
    }
}

internal sealed class FakeMatches : IMatchDirectory
{
    public Dictionary<Guid, MatchInfo> Items { get; } = [];

    public HashSet<Guid> Ended { get; } = [];

    public Task<MatchInfo?> FindAsync(Guid matchId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.GetValueOrDefault(matchId));

    public Task<IReadOnlyList<MatchSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MatchSummary>>(Items.Values
            .Where(match => match.IsParticipant(userId))
            .Select(match => new MatchSummary(match.Id, Ended.Contains(match.Id)))
            .ToList());

    public Task<IReadOnlySet<Guid>> GetUsersWithOpenMatchAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}

public class SessionHandlerTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly Guid _matchId = Guid.NewGuid();
    private readonly InMemorySessions _sessions = new();
    private readonly FakeMatches _matches = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();

    public SessionHandlerTests() => SetMatch(isActive: true);

    private void SetMatch(bool isActive, bool hasPendingEndRequest = false) =>
        _matches.Items[_matchId] = new MatchInfo(_matchId, _ali, _ayse, isActive, hasPendingEndRequest);

    private DateTimeOffset Tomorrow => _clock.GetUtcNow().AddDays(1);

    private Task<SwapSessionDto> ProposeAsync(
        Guid? userId = null,
        Guid? matchId = null,
        string? note = null,
        DateTimeOffset? at = null,
        int durationMinutes = 60) =>
        new ProposeSessionCommandHandler(_matches, _sessions, _notifier, _unitOfWork, _clock)
            .Handle(
                new ProposeSessionCommand(userId ?? _ali, matchId ?? _matchId, at ?? Tomorrow, durationMinutes, note),
                CancellationToken.None)
            .AsTask();

    /// <summary>A second match of each of the two users, with a third user.</summary>
    private (Guid AliCan, Guid AyseCan, Guid Can) AddOtherMatches()
    {
        var can = Guid.NewGuid();
        var aliCan = Guid.NewGuid();
        var ayseCan = Guid.NewGuid();
        _matches.Items[aliCan] = new MatchInfo(aliCan, _ali, can, true, false);
        _matches.Items[ayseCan] = new MatchInfo(ayseCan, _ayse, can, true, false);
        return (aliCan, ayseCan, can);
    }

    private Task<SwapSessionDto> ReportAsync(Guid? userId = null) =>
        new ReportInPersonSessionCommandHandler(_matches, _sessions, _notifier, _unitOfWork, _clock)
            .Handle(new ReportInPersonSessionCommand(userId ?? _ali, _matchId), CancellationToken.None).AsTask();

    private Task<SwapSessionDto> ChangeAsync(Guid sessionId, Guid userId, SessionAction action) =>
        new ChangeSessionCommandHandler(_sessions, _notifier, _unitOfWork, _clock)
            .Handle(new ChangeSessionCommand(userId, sessionId, action), CancellationToken.None).AsTask();

    private Task<SwapSessionDto> CancelAsync(Guid sessionId, Guid userId, string reason = "Uygun değilim") =>
        new CancelSessionCommandHandler(_sessions, _notifier, _unitOfWork, _clock)
            .Handle(new CancelSessionCommand(userId, sessionId, reason), CancellationToken.None).AsTask();

    private Task<SwapSessionDto> GetAsync(Guid sessionId, Guid userId) =>
        new GetSessionQueryHandler(_sessions, _clock).Handle(new GetSessionQuery(userId, sessionId), CancellationToken.None).AsTask();

    private async Task<SwapSessionDto> CompleteOnlineAsync()
    {
        var session = await ProposeAsync();
        await ChangeAsync(session.Id, _ayse, SessionAction.Accept);
        _clock.Advance(TimeSpan.FromDays(1));
        await ChangeAsync(session.Id, _ali, SessionAction.MarkCompleted);
        return await ChangeAsync(session.Id, _ayse, SessionAction.ConfirmCompleted);
    }

    private static async Task<string> AppCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<AppException>(action)).Code;

    private static async Task<string> DomainCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<DomainException>(action)).Code;

    [Fact]
    public async Task Propose_stores_an_online_proposal_addressed_to_the_other_user()
    {
        var dto = await ProposeAsync(note: "https://meet.example/abc");

        Assert.Equal(SwapSessionStatus.Proposed, dto.Status);
        Assert.Equal(SessionMode.Online, dto.Mode);
        Assert.Equal((_matchId, _ali, 60), (dto.MatchId, dto.ProposedByUserId, dto.DurationMinutes));
        Assert.Equal("https://meet.example/abc", dto.Note);
        Assert.Equal(_ayse, Assert.Single(_sessions.Items).OtherUserId);
    }

    [Fact]
    public async Task A_match_does_not_exist_for_anyone_but_its_two_users()
    {
        Assert.Equal("not_found", await AppCodeOfAsync(() => ProposeAsync(userId: Guid.NewGuid())));
        Assert.Equal("not_found", await AppCodeOfAsync(() => ProposeAsync(matchId: Guid.NewGuid())));
        Assert.Equal("not_found", await AppCodeOfAsync(() => ReportAsync(Guid.NewGuid())));
        Assert.Equal(
            "not_found",
            await AppCodeOfAsync(() => new ListMatchSessionsQueryHandler(_matches, _sessions, _clock)
                .Handle(new ListMatchSessionsQuery(Guid.NewGuid(), _matchId, null, null), CancellationToken.None).AsTask()));
        Assert.Empty(_sessions.Items);
    }

    [Theory]
    [InlineData(true, "match_end_pending")]
    [InlineData(false, "invalid_state")]
    public async Task Sessions_cannot_be_added_to_a_match_that_is_ending_or_over(bool endPending, string code)
    {
        SetMatch(isActive: false, hasPendingEndRequest: endPending);

        Assert.Equal(code, await AppCodeOfAsync(() => ProposeAsync()));
        Assert.Equal(code, await AppCodeOfAsync(() => ReportAsync()));
    }

    [Fact]
    public async Task Nobody_can_propose_two_sessions_at_the_same_time_in_one_match_or_across_matches()
    {
        var (aliCan, _, can) = AddOtherMatches();
        await ProposeAsync(at: Tomorrow, durationMinutes: 60);

        Assert.Equal("session_time_taken", await AppCodeOfAsync(() => ProposeAsync(at: Tomorrow.AddMinutes(30))));
        Assert.Equal(
            "session_time_taken",
            await AppCodeOfAsync(() => ProposeAsync(matchId: aliCan, at: Tomorrow.AddMinutes(-30), durationMinutes: 45)));

        // Ali's open proposal holds his time, though not Ayşe's yet.
        Assert.Equal("other_user_busy", await AppCodeOfAsync(() => ProposeAsync(userId: _ayse, at: Tomorrow)));
        Assert.Equal(
            "other_user_busy",
            await AppCodeOfAsync(() => ProposeAsync(userId: can, matchId: aliCan, at: Tomorrow)));
        await ProposeAsync(userId: can, matchId: aliCan, at: Tomorrow.AddHours(3));

        // Back to back is fine.
        await ProposeAsync(matchId: aliCan, at: Tomorrow.AddHours(1));
        await ProposeAsync(matchId: aliCan, at: Tomorrow.AddMinutes(-30), durationMinutes: 30);
        Assert.Equal(4, _sessions.Items.Count);
    }

    [Fact]
    public async Task A_proposal_is_refused_when_the_other_user_is_already_booked()
    {
        var (aliCan, ayseCan, can) = AddOtherMatches();
        var booked = await ProposeAsync(userId: can, matchId: ayseCan, at: Tomorrow);
        await ChangeAsync(booked.Id, _ayse, SessionAction.Accept);

        Assert.Equal("other_user_busy", await AppCodeOfAsync(() => ProposeAsync(at: Tomorrow.AddMinutes(15))));
        Assert.Equal("session_time_taken", await AppCodeOfAsync(() => ProposeAsync(userId: _ayse, at: Tomorrow.AddMinutes(15))));

        // Can is also booked, by the same session.
        Assert.Equal("other_user_busy", await AppCodeOfAsync(() => ProposeAsync(matchId: aliCan, at: Tomorrow)));
    }

    [Fact]
    public async Task Accepting_is_refused_when_either_user_has_meanwhile_planned_that_time()
    {
        var (aliCan, ayseCan, can) = AddOtherMatches();
        var fromAli = await ProposeAsync(at: Tomorrow);
        var fromCan = await ProposeAsync(userId: can, matchId: ayseCan, at: Tomorrow.AddMinutes(30));

        // Ayşe takes Can's session first; Ali's proposal at that time can no longer be accepted.
        await ChangeAsync(fromCan.Id, _ayse, SessionAction.Accept);
        Assert.Equal("session_time_taken", await AppCodeOfAsync(() => ChangeAsync(fromAli.Id, _ayse, SessionAction.Accept)));
        Assert.Equal(SwapSessionStatus.Proposed, (await GetAsync(fromAli.Id, _ali)).Status);

        // A proposer's own proposal holds their time, so they can only have been booked by
        // a race or by sessions planned before this rule; the answer is refused all the same.
        var later = await ProposeAsync(at: Tomorrow.AddHours(5));
        var clash = SwapSession.ProposeOnline(aliCan, can, _ali, Tomorrow.AddHours(5), 30, null, _clock.GetUtcNow());
        clash.Accept(_ali, _clock.GetUtcNow());
        _sessions.Add(clash);
        Assert.Equal("other_user_busy", await AppCodeOfAsync(() => ChangeAsync(later.Id, _ayse, SessionAction.Accept)));
    }

    [Fact]
    public async Task Declined_and_cancelled_sessions_free_their_time()
    {
        var declined = await ProposeAsync(at: Tomorrow);
        await ChangeAsync(declined.Id, _ayse, SessionAction.Decline);
        var cancelled = await ProposeAsync(at: Tomorrow);
        await ChangeAsync(cancelled.Id, _ayse, SessionAction.Accept);
        await CancelAsync(cancelled.Id, _ali);

        var again = await ProposeAsync(at: Tomorrow);
        Assert.Equal(SwapSessionStatus.Scheduled, (await ChangeAsync(again.Id, _ayse, SessionAction.Accept)).Status);
    }

    [Fact]
    public async Task An_online_session_runs_from_proposal_to_confirmed_completion()
    {
        var session = await ProposeAsync();

        Assert.Equal(SwapSessionStatus.Scheduled, (await ChangeAsync(session.Id, _ayse, SessionAction.Accept)).Status);
        Assert.Equal(
            SessionErrors.SessionNotStartedCode,
            await DomainCodeOfAsync(() => ChangeAsync(session.Id, _ali, SessionAction.MarkCompleted)));

        _clock.Advance(TimeSpan.FromDays(1));
        var marked = await ChangeAsync(session.Id, _ali, SessionAction.MarkCompleted);
        Assert.Equal(SwapSessionStatus.CompletionPending, marked.Status);
        Assert.Equal(_clock.GetUtcNow().AddHours(72), marked.CompletionConfirmDeadline);

        var completed = await ChangeAsync(session.Id, _ayse, SessionAction.ConfirmCompleted);
        Assert.Equal(SwapSessionStatus.Completed, completed.Status);
        Assert.False(completed.CompletedAutomatically);
    }

    [Fact]
    public async Task Only_the_right_user_can_answer_or_confirm_and_strangers_see_nothing()
    {
        var session = await ProposeAsync();
        var stranger = Guid.NewGuid();

        Assert.Equal(DomainErrorCodes.NotParticipant, await DomainCodeOfAsync(() => ChangeAsync(session.Id, _ali, SessionAction.Accept)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => ChangeAsync(session.Id, stranger, SessionAction.Accept)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => CancelAsync(session.Id, stranger)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => GetAsync(session.Id, stranger)));

        var reported = await ReportAsync();
        Assert.Equal(
            DomainErrorCodes.NotParticipant,
            await DomainCodeOfAsync(() => ChangeAsync(reported.Id, _ali, SessionAction.ConfirmCompleted)));
    }

    [Fact]
    public async Task An_in_person_meeting_is_reported_and_confirmed_without_a_time()
    {
        var reported = await ReportAsync(_ayse);

        Assert.Equal((SessionMode.InPerson, SwapSessionStatus.CompletionPending), (reported.Mode, reported.Status));
        Assert.Null(reported.ScheduledAt);
        Assert.Equal(_ayse, reported.CompletionMarkedByUserId);

        var completed = await ChangeAsync(reported.Id, _ali, SessionAction.ConfirmCompleted);
        Assert.Equal(SwapSessionStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task A_pair_can_have_only_one_in_person_meeting_that_was_not_cancelled()
    {
        var first = await ReportAsync();
        Assert.Equal("in_person_already_reported", await AppCodeOfAsync(() => ReportAsync(_ayse)));

        // A disputed report does not use up the pair's one meeting.
        await CancelAsync(first.Id, _ayse);
        var second = await ReportAsync(_ayse);
        await ChangeAsync(second.Id, _ali, SessionAction.ConfirmCompleted);

        // Nor does a later match between the same two users bring a new one.
        var rematch = Guid.NewGuid();
        _matches.Items[rematch] = new MatchInfo(rematch, _ayse, _ali, true, false);
        Assert.Equal(
            "in_person_already_reported",
            await AppCodeOfAsync(() => new ReportInPersonSessionCommandHandler(_matches, _sessions, _notifier, _unitOfWork, _clock)
                .Handle(new ReportInPersonSessionCommand(_ali, rematch), CancellationToken.None).AsTask()));
        Assert.Equal(2, _sessions.Items.Count);
    }

    [Fact]
    public async Task A_cancelled_session_carries_who_cancelled_and_why()
    {
        var session = await ProposeAsync();

        var cancelled = await CancelAsync(session.Id, _ayse, "  O gün şehir dışındayım.  ");

        Assert.Equal(SwapSessionStatus.Cancelled, cancelled.Status);
        Assert.Equal((_ayse, "O gün şehir dışındayım."), (cancelled.CancelledByUserId, cancelled.CancelReason));
    }

    [Fact]
    public async Task Passed_deadlines_show_at_once_and_are_recorded_by_the_deadline_run()
    {
        var ignored = await ProposeAsync();
        var reported = await ReportAsync();
        var handler = new ProcessSessionDeadlinesCommandHandler(_sessions, _notifier, _unitOfWork, _clock);
        Assert.Equal(0, await handler.Handle(new ProcessSessionDeadlinesCommand(), CancellationToken.None));

        _clock.Advance(TimeSpan.FromHours(72));

        // Readers see the outcome before anything has recorded it.
        Assert.Equal(SwapSessionStatus.Declined, (await GetAsync(ignored.Id, _ayse)).Status);
        var completed = await GetAsync(reported.Id, _ayse);
        Assert.Equal(SwapSessionStatus.Completed, completed.Status);
        Assert.True(completed.CompletedAutomatically);
        Assert.NotNull(completed.CompletedAt);
        Assert.Null(completed.CompletionConfirmDeadline);
        Assert.All(_sessions.Items, session => Assert.NotEqual(session.Status, session.StatusAt(_clock.GetUtcNow())));

        Assert.Equal(2, await handler.Handle(new ProcessSessionDeadlinesCommand(), CancellationToken.None));
        Assert.Equal(
            [SwapSessionStatus.Declined, SwapSessionStatus.Completed],
            _sessions.Items.Select(session => session.Status));
        Assert.Equal(0, await handler.Handle(new ProcessSessionDeadlinesCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Ending_the_match_cancels_only_the_sessions_still_ahead()
    {
        var proposed = await ProposeAsync();
        var scheduled = await ProposeAsync(at: Tomorrow.AddHours(2));
        await ChangeAsync(scheduled.Id, _ayse, SessionAction.Accept);
        var reported = await ReportAsync();

        await new CancelSessionsOnMatchEnded(_sessions, _clock).Handle(new MatchEnded(_matchId), CancellationToken.None);
        await new CancelSessionsOnMatchEnded(_sessions, _clock).Handle(new MatchEnded(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(SwapSessionStatus.Cancelled, (await GetAsync(proposed.Id, _ali)).Status);
        var cancelled = await GetAsync(scheduled.Id, _ali);
        Assert.Equal(SwapSessionStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.CancelledByUserId);
        Assert.Equal(SwapSessionStatus.CompletionPending, (await GetAsync(reported.Id, _ali)).Status);
    }

    [Fact]
    public async Task Each_step_of_a_session_tells_the_other_user()
    {
        var session = await ProposeAsync();
        var proposed = Assert.Single(_notifier.Sent);
        Assert.Equal((_ayse, NotificationType.SessionProposed), (proposed.UserId, proposed.Type));
        Assert.Equal((_ali, _matchId, session.Id), (proposed.Data.ActorUserId, proposed.Data.MatchId, proposed.Data.SessionId));
        Assert.Equal(Tomorrow, proposed.Data.Deadline);
        _notifier.Take();

        await ChangeAsync(session.Id, _ayse, SessionAction.Accept);
        _clock.Advance(TimeSpan.FromDays(1));
        await ChangeAsync(session.Id, _ayse, SessionAction.MarkCompleted);
        Assert.Equal(_clock.GetUtcNow() + SwapSession.ConfirmationPeriod, _notifier.Sent[^1].Data.Deadline);
        await ChangeAsync(session.Id, _ali, SessionAction.ConfirmCompleted);

        var declined = await ProposeAsync(_ayse);
        await ChangeAsync(declined.Id, _ali, SessionAction.Decline);
        var cancelled = await ProposeAsync();
        await CancelAsync(cancelled.Id, _ali, "Planım değişti.");
        Assert.Equal("Planım değişti.", _notifier.Sent[^1].Data.Reason);
        await ReportAsync(_ayse);

        Assert.Equal(
            [
                (_ali, NotificationType.SessionAccepted),
                (_ali, NotificationType.SessionCompletionMarked),
                (_ayse, NotificationType.SessionCompleted),
                (_ali, NotificationType.SessionProposed),
                (_ayse, NotificationType.SessionDeclined),
                (_ayse, NotificationType.SessionProposed),
                (_ayse, NotificationType.SessionCancelled),
                (_ali, NotificationType.SessionCompletionMarked),
            ],
            _notifier.Take());
    }

    [Fact]
    public async Task Deadline_processing_reminds_of_sessions_about_to_start_and_of_marks_about_to_stand()
    {
        var handler = new ProcessSessionDeadlinesCommandHandler(_sessions, _notifier, _unitOfWork, _clock);
        Task<int> RunAsync() => handler.Handle(new ProcessSessionDeadlinesCommand(), CancellationToken.None).AsTask();
        var session = SwapSession.ProposeOnline(_matchId, _ali, _ayse, _clock.GetUtcNow().AddDays(3), 60, null, _clock.GetUtcNow());
        session.Accept(_ayse, _clock.GetUtcNow());
        _sessions.Items.Add(session);

        Assert.Equal(0, await RunAsync());

        _clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(1, await RunAsync());
        Assert.Equal(0, await RunAsync());
        Assert.Equal([(_ali, NotificationType.SessionStartingSoon), (_ayse, NotificationType.SessionStartingSoon)], _notifier.Take());

        _clock.Advance(TimeSpan.FromHours(23.5));
        Assert.Equal(1, await RunAsync());
        Assert.Equal(session.ScheduledAt, _notifier.Sent[0].Data.Deadline);
        Assert.Equal(2, _notifier.Take().Count);

        // Marked as completed; nobody answers, so the other user is reminded and then it stands.
        _clock.Advance(TimeSpan.FromHours(1));
        session.MarkCompleted(_ali, _clock.GetUtcNow());
        _clock.Advance(TimeSpan.FromHours(50));
        Assert.Equal(1, await RunAsync());
        Assert.Equal(0, await RunAsync());
        var reminder = Assert.Single(_notifier.Sent);
        Assert.Equal((_ayse, NotificationType.SessionCompletionReminder), (reminder.UserId, reminder.Type));
        Assert.Equal(_ali, reminder.Data.ActorUserId);
        _notifier.Take();

        _clock.Advance(TimeSpan.FromHours(22));
        Assert.Equal(1, await RunAsync());
        Assert.Equal([(_ali, NotificationType.SessionCompleted), (_ayse, NotificationType.SessionCompleted)], _notifier.Take());

        // A proposal nobody answered: its author is told when it lapses.
        await ProposeAsync();
        _notifier.Take();
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, await RunAsync());
        Assert.Equal([(_ali, NotificationType.SessionDeclined)], _notifier.Take());
    }

    [Fact]
    public async Task Sessions_are_listed_per_match_and_per_user_with_filters()
    {
        var upcoming = await ProposeAsync();
        await ReportAsync();
        var perMatch = await new ListMatchSessionsQueryHandler(_matches, _sessions, _clock)
            .Handle(new ListMatchSessionsQuery(_ayse, _matchId, null, null), CancellationToken.None);
        var handler = new ListSessionsQueryHandler(_sessions, _clock);

        async Task<IReadOnlyList<SwapSessionDto>> ListAsync(Guid userId, SwapSessionStatus? status = null, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
            (await handler.Handle(new ListSessionsQuery(userId, status, from, to, null, null), CancellationToken.None)).Items;

        Assert.Equal(2, perMatch.TotalCount);
        Assert.Equal(2, (await ListAsync(_ali)).Count);
        Assert.Equal(upcoming.Id, Assert.Single(await ListAsync(_ayse, SwapSessionStatus.Proposed)).Id);
        Assert.Equal(SessionMode.InPerson, Assert.Single(await ListAsync(_ayse, SwapSessionStatus.CompletionPending)).Mode);
        Assert.Equal(upcoming.Id, Assert.Single(await ListAsync(_ali, from: _clock.GetUtcNow())).Id);
        Assert.Empty(await ListAsync(_ali, to: _clock.GetUtcNow()));
        Assert.Empty(await ListAsync(Guid.NewGuid()));
    }

    [Fact]
    public void Validators_reject_durations_notes_and_reasons_out_of_range()
    {
        var propose = new ProposeSessionCommandValidator();
        var cancel = new CancelSessionCommandValidator();

        Assert.True(propose.Validate(new ProposeSessionCommand(_ali, _matchId, Tomorrow, 90, new string('a', 500))).IsValid);
        Assert.False(propose.Validate(new ProposeSessionCommand(_ali, _matchId, Tomorrow, 50, null)).IsValid);
        Assert.False(propose.Validate(new ProposeSessionCommand(_ali, _matchId, Tomorrow, 60, new string('a', 501))).IsValid);
        Assert.True(cancel.Validate(new CancelSessionCommand(_ali, Guid.NewGuid(), "Uygun değilim")).IsValid);
        Assert.False(cancel.Validate(new CancelSessionCommand(_ali, Guid.NewGuid(), "  ok  ")).IsValid);
        Assert.False(cancel.Validate(new CancelSessionCommand(_ali, Guid.NewGuid(), new string('a', 501))).IsValid);
    }

    [Fact]
    public async Task Statistics_count_total_completed_and_cancelled_matches_and_completed_sessions()
    {
        var users = new FakeUserDirectory();
        users.Users[_ali] = new UserInfo(_ali, "ali", IsActive: true, IsEmailVerified: true);
        var stats = new UserStats(users, _matches, new SessionDirectory(_sessions, _clock));

        // Match 1 (this one): two completed sessions. While it runs it counts nowhere.
        await CompleteOnlineAsync();
        var second = await ReportAsync();
        await ChangeAsync(second.Id, _ayse, SessionAction.ConfirmCompleted);
        Assert.Equal(new UserStatsDto(0, 0, 0, 2), await stats.GetAsync(_ali, CancellationToken.None));

        // It ends: completed, because a session was completed in it.
        _matches.Ended.Add(_matchId);

        // Match 2 ended without any completed session; match 3 is still running.
        var cancelledMatch = Guid.NewGuid();
        _matches.Items[cancelledMatch] = new MatchInfo(cancelledMatch, _ali, Guid.NewGuid(), false, false);
        _matches.Ended.Add(cancelledMatch);
        var running = Guid.NewGuid();
        _matches.Items[running] = new MatchInfo(running, Guid.NewGuid(), _ali, true, false);

        Assert.Equal(new UserStatsDto(2, 1, 1, 2), await stats.GetAsync(_ali, CancellationToken.None));

        // Ayşe took part in the first match only.
        users.Users[_ayse] = new UserInfo(_ayse, "ayse", IsActive: true, IsEmailVerified: true);
        Assert.Equal(new UserStatsDto(1, 1, 0, 2), await stats.GetAsync(_ayse, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Statistics_of_an_unknown_or_inactive_user_are_not_found(bool known)
    {
        var users = new FakeUserDirectory();
        if (known)
        {
            users.Users[_ali] = new UserInfo(_ali, "ali", IsActive: false, IsEmailVerified: true);
        }

        var stats = new UserStats(users, _matches, new SessionDirectory(_sessions, _clock));

        Assert.Equal("not_found", await AppCodeOfAsync(() => stats.GetAsync(_ali, CancellationToken.None)));
    }
}
