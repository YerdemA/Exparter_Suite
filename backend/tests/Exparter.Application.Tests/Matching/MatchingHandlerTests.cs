using Exparter.Application.Moderation;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using AccountDeletionRequested = Exparter.Application.Identity.AccountDeletionRequested;
using Exparter.Application.Matching;
using Exparter.Application.Messaging;
using Exparter.Application.Tests.Messaging;
using Exparter.Application.Skills;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Common;
using Exparter.Domain.Matching;
using Exparter.Domain.Notifications;

namespace Exparter.Application.Tests.Matching;

internal sealed class InMemorySwapRequests : ISwapRequestRepository
{
    public List<SwapRequest> Items { get; } = [];

    public Task<SwapRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<SwapRequest?> FindPendingBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Status == SwapRequestStatus.Pending
            && x.IsParticipant(firstUserId)
            && x.IsParticipant(secondUserId)));

    public Task<SwapRequest?> FindLastRefusedAsync(Guid requesterId, Guid recipientId, CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(x => x.RequesterId == requesterId
                && x.RecipientId == recipientId
                && x.Status is SwapRequestStatus.Declined or SwapRequestStatus.Expired)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault());

    public Task<PagedList<SwapRequest>> ListAsync(
        Guid userId,
        SwapRequestDirection direction,
        SwapRequestStatus? status,
        Guid? otherUserId,
        DateTimeOffset now,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var all = Items
            .Where(x => (direction == SwapRequestDirection.Incoming ? x.RecipientId : x.RequesterId) == userId
                && (otherUserId is null || (direction == SwapRequestDirection.Incoming ? x.RequesterId : x.RecipientId) == otherUserId)
                && (status is null || x.StatusAt(now) == status))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        return Task.FromResult(new PagedList<SwapRequest>(all, page.Page, page.PageSize, all.Count));
    }

    public Task<IReadOnlyList<SwapRequest>> ListOverdueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwapRequest>>(
            Items.Where(x => x.Status == SwapRequestStatus.Pending && x.ExpiresAt <= now).Take(limit).ToList());

    public Task<bool> UsesOfferedSkillAsync(Guid userId, Guid skillId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.StatusAt(now) == SwapRequestStatus.Pending
            && ((x.RequesterId == userId && x.OfferedSkillId == skillId)
                || (x.RecipientId == userId && x.RequestedSkillId == skillId))));

    public Task<IReadOnlyList<SwapRequest>> ListPendingOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwapRequest>>(
            Items.Where(x => x.Status == SwapRequestStatus.Pending && x.IsParticipant(userId)).ToList());

    public void Add(SwapRequest request) => Items.Add(request);
}

internal sealed class InMemoryMatches : IMatchRepository
{
    public List<Match> Items { get; } = [];

    public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<Match?> FindOpenBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Status != MatchStatus.Ended && x.IsParticipant(firstUserId) && x.IsParticipant(secondUserId)));

    public Task<bool> HasOpenBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.Status != MatchStatus.Ended && x.IsParticipant(firstUserId) && x.IsParticipant(secondUserId)));

    public Task<PagedList<Match>> ListAsync(Guid userId, MatchStatus? status, Guid? otherUserId, PageRequest page, CancellationToken cancellationToken)
    {
        var all = Items
            .Where(x => x.IsParticipant(userId)
                && (otherUserId is null || x.IsParticipant(otherUserId.Value))
                && (status is null || x.Status == status))
            .ToList();
        return Task.FromResult(new PagedList<Match>(all, page.Page, page.PageSize, all.Count));
    }

    public Task<IReadOnlyList<Match>> ListAllAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Match>>(Items.Where(x => x.IsParticipant(userId)).ToList());

    public Task<IReadOnlyList<Match>> ListWithOverdueEndRequestAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Match>>(
            Items.Where(x => x.Status == MatchStatus.EndRequested && x.EndRequestExpiresAt <= now).Take(limit).ToList());

    public Task<IReadOnlyList<Match>> ListDueForEndReminderAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Match>>(Items
            .Where(x => x.Status == MatchStatus.EndRequested
                && x.EndReminderSentAt is null
                && x.EndRequestExpiresAt > now
                && x.EndRequestExpiresAt <= now + Match.EndReminderLeadTime)
            .Take(limit)
            .ToList());

    public Task<bool> UsesOfferedSkillAsync(Guid userId, Guid skillId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.Status != MatchStatus.Ended
            && ((x.UserAId == userId && x.SkillTaughtByAId == skillId) || (x.UserBId == userId && x.SkillTaughtByBId == skillId))));

    public Task<IReadOnlySet<Guid>> ListOpenPartnersAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(Items
            .Where(x => x.Status != MatchStatus.Ended && x.IsParticipant(userId))
            .Select(x => x.UserAId == userId ? x.UserBId : x.UserAId)
            .Where(otherUserIds.Contains)
            .ToHashSet());

    public Task<IReadOnlyList<Match>> ListOpenOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Match>>(Items.Where(x => x.Status != MatchStatus.Ended && x.IsParticipant(userId)).ToList());

    public void Add(Match match) => Items.Add(match);
}

internal sealed class RecordingNotifier : Exparter.Application.Notifications.IUserNotifier
{
    public List<(Guid UserId, NotificationType Type, NotificationData Data)> Sent { get; } = [];

    public void Notify(Guid userId, NotificationType type, NotificationData data) => Sent.Add((userId, type, data));

    /// <summary>What was sent since the last call, as recipient and type.</summary>
    public List<(Guid UserId, NotificationType Type)> Take()
    {
        var taken = Sent.Select(item => (item.UserId, item.Type)).ToList();
        Sent.Clear();
        return taken;
    }
}

internal sealed class RecordingPublisher : Mediator.IPublisher
{
    public List<object> Published { get; } = [];

    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : Mediator.INotification
    {
        Published.Add(notification);
        return ValueTask.CompletedTask;
    }

    public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
    {
        Published.Add(notification);
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeInteractionRequirements : IInteractionRequirements
{
    public HashSet<Guid> CannotInteract { get; } = [];

    public Task<InteractionStatus> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var can = !CannotInteract.Contains(userId);
        return Task.FromResult(new InteractionStatus(can, can, can, can));
    }

    public Task EnsureCanInteractAsync(Guid userId, CancellationToken cancellationToken) =>
        CannotInteract.Contains(userId)
            ? throw AppException.Forbidden("interaction_requirements_not_met", "Not met.")
            : Task.CompletedTask;
}

internal sealed class FakeUserSkillDirectory : IUserSkillDirectory
{
    public HashSet<(Guid UserId, Guid SkillId)> Offered { get; } = [];

    public Dictionary<Guid, string> Names { get; } = [];

    public Task<bool> HasOfferedSkillAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Offered.Any(x => x.UserId == userId));

    public Task<bool> OffersSkillAsync(Guid userId, Guid skillId, CancellationToken cancellationToken) =>
        Task.FromResult(Offered.Contains((userId, skillId)));

    public Task<IReadOnlyDictionary<Guid, string>> GetSkillNamesAsync(IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Names.Where(x => skillIds.Contains(x.Key)).ToDictionary());
}

internal sealed class FakeUserSummaries(FakeUserDirectory users) : IUserSummaries
{
    public Task<IReadOnlyDictionary<Guid, UserSummaryDto>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UserSummaryDto>>(userIds.Distinct().ToDictionary(
            id => id,
            id => new UserSummaryDto(id, users.Users.GetValueOrDefault(id)?.Username, null, null, !users.Users.ContainsKey(id))));
}

public class MatchingHandlerTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly Guid _guitar = Guid.NewGuid();
    private readonly Guid _spanish = Guid.NewGuid();
    private readonly InMemorySwapRequests _requests = new();
    private readonly InMemoryMatches _matches = new();
    private readonly FakeUserDirectory _users = new();
    private readonly FakeUserSkillDirectory _skills = new();
    private readonly FakeInteractionRequirements _requirements = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();
    private readonly InMemoryConversations _conversations = new();
    private readonly RecordingPublisher _publisher = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly MatchingDtoFactory _dtos;

    public MatchingHandlerTests()
    {
        _users.Users[_ali] = new UserInfo(_ali, "ali", IsActive: true, IsEmailVerified: true);
        _users.Users[_ayse] = new UserInfo(_ayse, "ayse", IsActive: true, IsEmailVerified: true);
        _skills.Offered.Add((_ali, _guitar));
        _skills.Offered.Add((_ayse, _spanish));
        _skills.Names[_guitar] = "Gitar";
        _skills.Names[_spanish] = "İspanyolca";
        _dtos = new MatchingDtoFactory(new FakeUserSummaries(_users), _skills, new ConversationDirectory(_conversations), _clock);
    }

    private SendSwapRequestCommandHandler SendHandler =>
        new(_requirements, _users, _skills, new BlockDirectory(Blocks), _requests, _matches, _dtos, _notifier, _unitOfWork, _clock);

    internal Moderation.InMemoryBlocks Blocks { get; } = new();

    /// <summary>Ali offers the guitar and asks Ayşe for Spanish.</summary>
    private Task<SwapRequestDto> SendAsync(string? message = null) =>
        SendHandler.Handle(new SendSwapRequestCommand(_ali, _ayse, _guitar, _spanish, message), CancellationToken.None).AsTask();

    private Task<SwapRequestDto> SendBackAsync() =>
        SendHandler.Handle(new SendSwapRequestCommand(_ayse, _ali, _spanish, _guitar, null), CancellationToken.None).AsTask();

    private Task<MatchDto> AcceptAsync(Guid requestId, Guid? userId = null) =>
        new AcceptSwapRequestCommandHandler(_requirements, _requests, _matches, _dtos, _publisher, _notifier, _unitOfWork, _clock)
            .Handle(new AcceptSwapRequestCommand(userId ?? _ayse, requestId), CancellationToken.None).AsTask();

    private Task<SwapRequestDto> DeclineAsync(Guid requestId, Guid? userId = null) =>
        new DeclineSwapRequestCommandHandler(_requests, _dtos, _notifier, _unitOfWork, _clock)
            .Handle(new DeclineSwapRequestCommand(userId ?? _ayse, requestId), CancellationToken.None).AsTask();

    private Task<SwapRequestDto> CancelAsync(Guid requestId, Guid? userId = null) =>
        new CancelSwapRequestCommandHandler(_requests, _dtos, _notifier, _unitOfWork, _clock)
            .Handle(new CancelSwapRequestCommand(userId ?? _ali, requestId), CancellationToken.None).AsTask();

    private Task<MatchDto> ChangeEndAsync(Guid matchId, Guid userId, MatchEndAction action) =>
        new ChangeMatchEndCommandHandler(_matches, _dtos, _publisher, _notifier, _unitOfWork, _clock)
            .Handle(new ChangeMatchEndCommand(userId, matchId, action), CancellationToken.None).AsTask();

    private Task<int> ProcessDeadlinesAsync() =>
        new ProcessMatchingDeadlinesCommandHandler(_requests, _matches, _publisher, _notifier, _unitOfWork, _clock)
            .Handle(new ProcessMatchingDeadlinesCommand(), CancellationToken.None).AsTask();

    private static async Task<string> AppCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<AppException>(action)).Code;

    private static async Task<string> DomainCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<DomainException>(action)).Code;

    [Fact]
    public async Task Send_stores_a_pending_request_and_describes_both_users_and_skills()
    {
        var dto = await SendAsync("Merhaba");

        Assert.Equal(SwapRequestStatus.Pending, dto.Status);
        Assert.Equal(("ali", "ayse"), (dto.Requester.Username, dto.Recipient.Username));
        Assert.Equal(("Gitar", "İspanyolca"), (dto.OfferedSkill.Name, dto.RequestedSkill.Name));
        Assert.Equal("Merhaba", dto.Message);
        Assert.Equal(_clock.GetUtcNow().AddDays(3), dto.ExpiresAt);
        Assert.Single(_requests.Items);
    }

    [Fact]
    public async Task Send_requires_the_sender_to_meet_the_interaction_requirements()
    {
        _requirements.CannotInteract.Add(_ali);

        Assert.Equal("interaction_requirements_not_met", await AppCodeOfAsync(() => SendAsync()));
        Assert.Empty(_requests.Items);
    }

    [Fact]
    public async Task Send_to_a_recipient_who_cannot_interact_yet_is_allowed()
    {
        _requirements.CannotInteract.Add(_ayse);

        Assert.Equal(SwapRequestStatus.Pending, (await SendAsync()).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Send_to_an_unknown_or_inactive_user_is_not_found(bool known)
    {
        if (known)
        {
            _users.Users[_ayse] = new UserInfo(_ayse, "ayse", IsActive: false, IsEmailVerified: true);
        }
        else
        {
            _users.Users.Remove(_ayse);
        }

        Assert.Equal("not_found", await AppCodeOfAsync(() => SendAsync()));
    }

    [Fact]
    public async Task Send_to_oneself_is_rejected() =>
        Assert.Equal(
            MatchingErrors.SwapRequestToSelfCode,
            await DomainCodeOfAsync(() => SendHandler
                .Handle(new SendSwapRequestCommand(_ali, _ali, _guitar, _guitar, null), CancellationToken.None).AsTask()));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Send_requires_each_side_to_offer_the_skill_they_would_teach(bool senderLacksSkill)
    {
        _skills.Offered.Remove(senderLacksSkill ? (_ali, _guitar) : (_ayse, _spanish));

        Assert.Equal("skill_not_offered", await AppCodeOfAsync(() => SendAsync()));
    }

    [Fact]
    public async Task A_pair_can_have_only_one_pending_request_whoever_sent_it()
    {
        await SendAsync();

        Assert.Equal("swap_request_already_pending", await AppCodeOfAsync(() => SendAsync()));
        Assert.Equal("swap_request_already_pending", await AppCodeOfAsync(SendBackAsync));
    }

    [Fact]
    public async Task A_pair_with_an_unfinished_match_cannot_exchange_requests()
    {
        await AcceptAsync((await SendAsync()).Id);

        Assert.Equal("match_already_exists", await AppCodeOfAsync(() => SendAsync()));
        Assert.Equal("match_already_exists", await AppCodeOfAsync(SendBackAsync));
    }

    [Fact]
    public async Task A_declined_requester_waits_seven_days_but_the_recipient_may_ask_right_away()
    {
        await DeclineAsync((await SendAsync()).Id);

        Assert.Equal(MatchingErrors.SwapRequestCooldownCode, await DomainCodeOfAsync(() => SendAsync()));
        Assert.Equal(SwapRequestStatus.Pending, (await SendBackAsync()).Status);
    }

    [Fact]
    public async Task After_the_wait_a_declined_requester_can_ask_again()
    {
        await DeclineAsync((await SendAsync()).Id);
        _clock.Advance(TimeSpan.FromDays(7));

        Assert.Equal(SwapRequestStatus.Pending, (await SendAsync()).Status);
    }

    [Fact]
    public async Task A_withdrawn_request_can_be_sent_again_right_away()
    {
        await CancelAsync((await SendAsync()).Id);

        Assert.Equal(SwapRequestStatus.Pending, (await SendAsync()).Status);
    }

    [Fact]
    public async Task An_ignored_request_whose_expiry_was_not_recorded_yet_still_starts_the_wait()
    {
        await SendAsync();
        _clock.Advance(TimeSpan.FromDays(4));

        Assert.Equal(MatchingErrors.SwapRequestCooldownCode, await DomainCodeOfAsync(() => SendAsync()));
        Assert.Equal(SwapRequestStatus.Expired, Assert.Single(_requests.Items).Status);
    }

    [Fact]
    public async Task An_ignored_request_does_not_block_the_other_user_from_asking()
    {
        await SendAsync();
        _clock.Advance(TimeSpan.FromDays(4));

        Assert.Equal(SwapRequestStatus.Pending, (await SendBackAsync()).Status);
        Assert.Equal(
            [SwapRequestStatus.Expired, SwapRequestStatus.Pending],
            _requests.Items.Select(request => request.Status));
    }

    [Fact]
    public async Task Accept_creates_the_match_between_the_two_users()
    {
        var request = await SendAsync();

        var match = await AcceptAsync(request.Id);

        Assert.Equal(MatchStatus.Active, match.Status);
        Assert.Equal(request.Id, match.SwapRequestId);
        Assert.Equal(("ali", "ayse"), (match.UserA.Username, match.UserB.Username));
        Assert.Equal(("Gitar", "İspanyolca"), (match.SkillTaughtByA.Name, match.SkillTaughtByB.Name));
        var created = Assert.IsType<MatchCreated>(Assert.Single(_publisher.Published));
        Assert.Equal((match.Id, _ali, _ayse), (created.MatchId, created.UserAId, created.UserBId));
        Assert.Equal(SwapRequestStatus.Accepted, Assert.Single(_requests.Items).Status);
        Assert.Single(_matches.Items);
    }

    [Fact]
    public async Task A_match_carries_the_id_of_the_pairs_conversation_for_either_viewer()
    {
        var conversation = Exparter.Domain.Messaging.Conversation.Open(_ayse, _ali, _clock.GetUtcNow());
        _conversations.Add(conversation);
        var match = await AcceptAsync((await SendAsync()).Id);
        var handler = new GetMatchQueryHandler(_matches, _dtos);

        Assert.Equal(conversation.Id, match.ConversationId);
        Assert.Equal(conversation.Id, (await handler.Handle(new GetMatchQuery(_ali, match.Id), CancellationToken.None)).ConversationId);
        Assert.Equal(conversation.Id, (await handler.Handle(new GetMatchQuery(_ayse, match.Id), CancellationToken.None)).ConversationId);
    }

    [Fact]
    public async Task The_match_directory_describes_a_match_and_lists_a_users_history()
    {
        var directory = new MatchDirectory(_matches);
        var match = await AcceptAsync((await SendAsync()).Id);

        var info = await directory.FindAsync(match.Id, CancellationToken.None);
        Assert.Equal(new MatchInfo(match.Id, _ali, _ayse, IsActive: true, HasPendingEndRequest: false), info);
        Assert.Equal(_ayse, info!.OtherThan(_ali));
        Assert.False(info.IsParticipant(Guid.NewGuid()));
        Assert.Null(await directory.FindAsync(Guid.NewGuid(), CancellationToken.None));

        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        Assert.Equal(
            (false, true),
            await directory.FindAsync(match.Id, CancellationToken.None) is { } pending ? (pending.IsActive, pending.HasPendingEndRequest) : default);
        Assert.Equal([new MatchSummary(match.Id, IsEnded: false)], await directory.ListForUserAsync(_ayse, CancellationToken.None));

        await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Accept);
        Assert.Equal([new MatchSummary(match.Id, IsEnded: true)], await directory.ListForUserAsync(_ali, CancellationToken.None));
        Assert.Empty(await directory.ListForUserAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task The_match_directory_names_the_users_someone_is_still_matched_with()
    {
        var directory = new MatchDirectory(_matches);
        var match = await AcceptAsync((await SendAsync()).Id);

        Assert.Equal([_ayse], await directory.GetUsersWithOpenMatchAsync(_ali, [_ayse, Guid.NewGuid()], CancellationToken.None));
        Assert.Equal([_ali], await directory.GetUsersWithOpenMatchAsync(_ayse, [_ali], CancellationToken.None));

        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Accept);
        Assert.Empty(await directory.GetUsersWithOpenMatchAsync(_ali, [_ayse], CancellationToken.None));
    }

    [Fact]
    public async Task Accept_requires_the_recipient_to_meet_the_interaction_requirements()
    {
        var request = await SendAsync();
        _requirements.CannotInteract.Add(_ayse);

        Assert.Equal("interaction_requirements_not_met", await AppCodeOfAsync(() => AcceptAsync(request.Id)));
        Assert.Empty(_matches.Items);
    }

    [Fact]
    public async Task Only_the_recipient_answers_and_only_the_requester_withdraws()
    {
        var request = await SendAsync();

        Assert.Equal(MatchingErrors.NotParticipantCode, await DomainCodeOfAsync(() => AcceptAsync(request.Id, _ali)));
        Assert.Equal(MatchingErrors.NotParticipantCode, await DomainCodeOfAsync(() => DeclineAsync(request.Id, _ali)));
        Assert.Equal(MatchingErrors.NotParticipantCode, await DomainCodeOfAsync(() => CancelAsync(request.Id, _ayse)));
    }

    [Fact]
    public async Task A_request_does_not_exist_for_anyone_but_its_two_users()
    {
        var request = await SendAsync();
        var stranger = Guid.NewGuid();

        Assert.Equal("not_found", await AppCodeOfAsync(() => AcceptAsync(request.Id, stranger)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => CancelAsync(request.Id, stranger)));
        Assert.Equal(
            "not_found",
            await AppCodeOfAsync(() => new GetSwapRequestQueryHandler(_requests, _dtos)
                .Handle(new GetSwapRequestQuery(stranger, request.Id), CancellationToken.None).AsTask()));
        Assert.Equal("not_found", await AppCodeOfAsync(() => DeclineAsync(Guid.NewGuid())));
    }

    [Fact]
    public async Task A_request_past_its_deadline_cannot_be_accepted_and_is_listed_as_expired()
    {
        var request = await SendAsync();
        _clock.Advance(TimeSpan.FromDays(3));

        Assert.Equal(MatchingErrors.InvalidStateCode, await DomainCodeOfAsync(() => AcceptAsync(request.Id)));

        var handler = new ListSwapRequestsQueryHandler(_requests, _dtos, _clock);
        var pending = await handler.Handle(
            new ListSwapRequestsQuery(_ayse, SwapRequestDirection.Incoming, SwapRequestStatus.Pending, null, null, null), CancellationToken.None);
        var expired = await handler.Handle(
            new ListSwapRequestsQuery(_ayse, SwapRequestDirection.Incoming, SwapRequestStatus.Expired, null, null, null), CancellationToken.None);
        Assert.Empty(pending.Items);
        Assert.Equal(SwapRequestStatus.Expired, Assert.Single(expired.Items).Status);
    }

    [Fact]
    public async Task Requests_are_listed_by_direction()
    {
        await SendAsync();
        var handler = new ListSwapRequestsQueryHandler(_requests, _dtos, _clock);

        async Task<int> CountAsync(Guid userId, SwapRequestDirection direction) =>
            (await handler.Handle(new ListSwapRequestsQuery(userId, direction, null, null, null, null), CancellationToken.None)).TotalCount;

        Assert.Equal(1, await CountAsync(_ali, SwapRequestDirection.Outgoing));
        Assert.Equal(0, await CountAsync(_ali, SwapRequestDirection.Incoming));
        Assert.Equal(1, await CountAsync(_ayse, SwapRequestDirection.Incoming));
        Assert.Equal(0, await CountAsync(_ayse, SwapRequestDirection.Outgoing));
    }

    [Fact]
    public async Task A_match_ends_when_one_user_asks_and_the_other_agrees()
    {
        var match = await AcceptAsync((await SendAsync()).Id);

        var waiting = await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        Assert.Equal(MatchStatus.EndRequested, waiting.Status);
        Assert.Equal(_ali, waiting.EndRequestedByUserId);
        Assert.Equal(_clock.GetUtcNow().AddDays(7), waiting.EndRequestExpiresAt);

        Assert.DoesNotContain(_publisher.Published, published => published is MatchEnded);
        var ended = await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Accept);
        Assert.Equal(MatchStatus.Ended, ended.Status);
        Assert.Equal(MatchEndReason.MutualConsent, ended.EndReason);
        Assert.Equal(new MatchEnded(match.Id), Assert.Single(_publisher.Published.OfType<MatchEnded>()));

        // The pair is free to match again.
        Assert.Equal(SwapRequestStatus.Pending, (await SendBackAsync()).Status);
    }

    [Theory]
    [InlineData(MatchEndAction.Reject)]
    [InlineData(MatchEndAction.Withdraw)]
    public async Task A_rejected_or_withdrawn_end_request_leaves_the_match_active(MatchEndAction action)
    {
        var match = await AcceptAsync((await SendAsync()).Id);
        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);

        var active = await ChangeEndAsync(match.Id, action == MatchEndAction.Reject ? _ayse : _ali, action);

        Assert.Equal(MatchStatus.Active, active.Status);
        Assert.Null(active.EndRequestedByUserId);
    }

    [Fact]
    public async Task Only_the_other_user_answers_an_end_request()
    {
        var match = await AcceptAsync((await SendAsync()).Id);
        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);

        Assert.Equal(MatchingErrors.NotParticipantCode, await DomainCodeOfAsync(() => ChangeEndAsync(match.Id, _ali, MatchEndAction.Accept)));
        Assert.Equal(MatchingErrors.NotParticipantCode, await DomainCodeOfAsync(() => ChangeEndAsync(match.Id, _ayse, MatchEndAction.Withdraw)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => ChangeEndAsync(match.Id, Guid.NewGuid(), MatchEndAction.Accept)));
    }

    [Fact]
    public async Task An_end_request_past_its_deadline_can_no_longer_be_rejected()
    {
        var match = await AcceptAsync((await SendAsync()).Id);
        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        _clock.Advance(TimeSpan.FromDays(7));

        Assert.Equal(MatchingErrors.InvalidStateCode, await DomainCodeOfAsync(() => ChangeEndAsync(match.Id, _ayse, MatchEndAction.Reject)));
    }

    [Fact]
    public async Task Matches_are_listed_for_both_users_and_filtered_by_status()
    {
        var match = await AcceptAsync((await SendAsync()).Id);
        var handler = new ListMatchesQueryHandler(_matches, _dtos);

        var forAyse = await handler.Handle(new ListMatchesQuery(_ayse, MatchStatus.Active, null, null, null), CancellationToken.None);
        var ended = await handler.Handle(new ListMatchesQuery(_ali, MatchStatus.Ended, null, null, null), CancellationToken.None);
        var single = await new GetMatchQueryHandler(_matches, _dtos).Handle(new GetMatchQuery(_ali, match.Id), CancellationToken.None);

        Assert.Equal(match.Id, Assert.Single(forAyse.Items).Id);
        Assert.Empty(ended.Items);
        Assert.Equal(match.Id, single.Id);
        Assert.Equal(
            "not_found",
            await AppCodeOfAsync(() => new GetMatchQueryHandler(_matches, _dtos)
                .Handle(new GetMatchQuery(Guid.NewGuid(), match.Id), CancellationToken.None).AsTask()));
    }

    [Fact]
    public async Task Each_step_of_a_request_and_of_ending_a_match_tells_the_other_user()
    {
        var declined = await SendAsync();
        Assert.Equal([(_ayse, NotificationType.SwapRequestReceived)], _notifier.Take());
        await DeclineAsync(declined.Id);
        Assert.Equal([(_ali, NotificationType.SwapRequestDeclined)], _notifier.Take());

        var withdrawn = await SendBackAsync();
        await CancelAsync(withdrawn.Id, _ayse);
        Assert.Equal([(_ali, NotificationType.SwapRequestReceived), (_ali, NotificationType.SwapRequestCancelled)], _notifier.Take());

        var request = await SendBackAsync();
        _notifier.Take();
        var match = await AcceptAsync(request.Id, _ali);
        var accepted = Assert.Single(_notifier.Sent);
        Assert.Equal((_ayse, NotificationType.SwapRequestAccepted), (accepted.UserId, accepted.Type));
        Assert.Equal((_ali, request.Id, match.Id), (accepted.Data.ActorUserId, accepted.Data.SwapRequestId, accepted.Data.MatchId));
        _notifier.Take();

        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        var asked = Assert.Single(_notifier.Sent);
        Assert.Equal((_ayse, NotificationType.MatchEndRequested), (asked.UserId, asked.Type));
        Assert.Equal(_clock.GetUtcNow() + Match.EndRequestLifetime, asked.Data.Deadline);
        _notifier.Take();

        await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Reject);
        await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Request);
        await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Withdraw);
        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Accept);

        Assert.Equal(
            [
                (_ali, NotificationType.MatchEndRequestRejected),
                (_ali, NotificationType.MatchEndRequested),
                (_ali, NotificationType.MatchEndRequestWithdrawn),
                (_ayse, NotificationType.MatchEndRequested),
                (_ali, NotificationType.MatchEnded),
            ],
            _notifier.Take());
    }

    [Fact]
    public async Task A_leaving_user_withdraws_from_pending_requests_and_ends_their_matches()
    {
        var carol = Guid.NewGuid();
        var deniz = Guid.NewGuid();
        foreach (var (id, name) in new[] { (carol, "carol"), (deniz, "deniz") })
        {
            _users.Users[id] = new UserInfo(id, name, IsActive: true, IsEmailVerified: true);
            _skills.Offered.Add((id, _spanish));
        }

        var match = await AcceptAsync((await SendAsync()).Id);
        var sent = await SendHandler.Handle(new SendSwapRequestCommand(_ali, carol, _guitar, _spanish, null), CancellationToken.None);
        var received = await SendHandler.Handle(new SendSwapRequestCommand(deniz, _ali, _spanish, _guitar, null), CancellationToken.None);
        var unrelated = await SendHandler.Handle(new SendSwapRequestCommand(deniz, carol, _spanish, _spanish, null), CancellationToken.None);
        _notifier.Take();
        _publisher.Published.Clear();

        await new CloseMatchingOnAccountDeletion(_requests, _matches, _publisher, _notifier, _clock)
            .Handle(new AccountDeletionRequested(_ali), CancellationToken.None);

        SwapRequestStatus StatusOf(Guid id) => _requests.Items.Single(x => x.Id == id).Status;
        Assert.Equal(SwapRequestStatus.Cancelled, StatusOf(sent.Id));
        Assert.Equal(SwapRequestStatus.Cancelled, StatusOf(received.Id));
        Assert.Equal(SwapRequestStatus.Pending, StatusOf(unrelated.Id));
        var ended = _matches.Items.Single(x => x.Id == match.Id);
        Assert.Equal((MatchStatus.Ended, MatchEndReason.AccountDeleted), (ended.Status, ended.EndReason));
        Assert.Equal(new MatchEnded(match.Id), Assert.Single(_publisher.Published));
        Assert.Equal(
            [
                (carol, NotificationType.SwapRequestCancelled),
                (deniz, NotificationType.SwapRequestExpired),
                (_ayse, NotificationType.MatchEnded),
            ],
            _notifier.Take());

        // Neither side waits before asking again: nobody refused anybody.
        _users.Users[_ali] = _users.Users[_ali] with { IsActive = true };
        await SendHandler.Handle(new SendSwapRequestCommand(deniz, _ali, _spanish, _guitar, null), CancellationToken.None);
    }

    [Fact]
    public async Task A_refused_action_tells_nobody()
    {
        var request = await SendAsync();
        _notifier.Take();

        await Assert.ThrowsAsync<DomainException>(() => DeclineAsync(request.Id, _ali));
        await Assert.ThrowsAsync<AppException>(() => SendAsync());

        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Deadline_processing_tells_of_what_lapsed_and_reminds_once_of_an_end_request_in_its_last_day()
    {
        var match = await AcceptAsync((await SendAsync()).Id);
        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        _notifier.Take();

        _clock.Advance(Match.EndRequestLifetime - TimeSpan.FromHours(30));
        Assert.Equal(0, await ProcessDeadlinesAsync());

        _clock.Advance(TimeSpan.FromHours(7));
        Assert.Equal(1, await ProcessDeadlinesAsync());
        Assert.Equal(0, await ProcessDeadlinesAsync());
        var reminder = Assert.Single(_notifier.Sent);
        Assert.Equal((_ayse, NotificationType.MatchEndReminder), (reminder.UserId, reminder.Type));
        Assert.Equal((_ali, match.Id), (reminder.Data.ActorUserId, reminder.Data.MatchId));
        Assert.NotNull(reminder.Data.Deadline);
        _notifier.Take();

        _clock.Advance(TimeSpan.FromHours(23));
        Assert.Equal(1, await ProcessDeadlinesAsync());
        Assert.Equal([(_ali, NotificationType.MatchEnded), (_ayse, NotificationType.MatchEnded)], _notifier.Take());

        // A request nobody answered: its sender is told when it lapses.
        var ignored = await SendAsync();
        _notifier.Take();
        _clock.Advance(TimeSpan.FromDays(3));
        Assert.Equal(1, await ProcessDeadlinesAsync());
        var expired = Assert.Single(_notifier.Sent);
        Assert.Equal((_ali, NotificationType.SwapRequestExpired, ignored.Id), (expired.UserId, expired.Type, expired.Data.SwapRequestId!.Value));
    }

    [Fact]
    public async Task Deadline_processing_expires_ignored_requests_and_ends_matches_with_unanswered_end_requests()
    {
        var match = await AcceptAsync((await SendAsync()).Id);
        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        var carol = Guid.NewGuid();
        _users.Users[carol] = new UserInfo(carol, "carol", IsActive: true, IsEmailVerified: true);
        _skills.Offered.Add((carol, _spanish));
        await SendHandler.Handle(new SendSwapRequestCommand(_ali, carol, _guitar, _spanish, null), CancellationToken.None);

        Assert.Equal(0, await ProcessDeadlinesAsync());

        _clock.Advance(TimeSpan.FromDays(7));
        Assert.Equal(2, await ProcessDeadlinesAsync());
        Assert.Equal(MatchStatus.Ended, Assert.Single(_matches.Items).Status);
        Assert.Equal(MatchEndReason.EndRequestExpired, _matches.Items[0].EndReason);
        Assert.Equal(new MatchEnded(match.Id), Assert.Single(_publisher.Published.OfType<MatchEnded>()));
        Assert.Equal(SwapRequestStatus.Expired, _requests.Items.Last().Status);
        Assert.Equal(0, await ProcessDeadlinesAsync());
    }

    [Fact]
    public async Task A_skill_is_in_use_while_a_pending_request_or_an_unfinished_match_relies_on_it()
    {
        var usage = new SkillUsage(_requests, _matches, _clock);

        async Task<(bool Guitar, bool Spanish)> InUseAsync() =>
            (await usage.IsOfferedSkillInUseAsync(_ali, _guitar, CancellationToken.None),
             await usage.IsOfferedSkillInUseAsync(_ayse, _spanish, CancellationToken.None));

        Assert.Equal((false, false), await InUseAsync());

        var request = await SendAsync();
        Assert.Equal((true, true), await InUseAsync());
        // The other user's side of the pair is a different skill.
        Assert.False(await usage.IsOfferedSkillInUseAsync(_ayse, _guitar, CancellationToken.None));

        var match = await AcceptAsync(request.Id);
        Assert.Equal((true, true), await InUseAsync());

        await ChangeEndAsync(match.Id, _ali, MatchEndAction.Request);
        await ChangeEndAsync(match.Id, _ayse, MatchEndAction.Accept);
        Assert.Equal((false, false), await InUseAsync());
    }

    [Fact]
    public void Send_validator_limits_the_message_and_requires_every_id()
    {
        var validator = new SendSwapRequestCommandValidator();

        Assert.True(validator.Validate(new SendSwapRequestCommand(_ali, _ayse, _guitar, _spanish, new string('a', 300))).IsValid);
        Assert.False(validator.Validate(new SendSwapRequestCommand(_ali, _ayse, _guitar, _spanish, new string('a', 301))).IsValid);
        Assert.False(validator.Validate(new SendSwapRequestCommand(_ali, Guid.Empty, _guitar, _spanish, null)).IsValid);
        Assert.False(validator.Validate(new SendSwapRequestCommand(_ali, _ayse, Guid.Empty, _spanish, null)).IsValid);
        Assert.False(validator.Validate(new SendSwapRequestCommand(_ali, _ayse, _guitar, Guid.Empty, null)).IsValid);
    }
}
