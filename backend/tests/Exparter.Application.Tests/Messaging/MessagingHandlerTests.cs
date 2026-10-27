using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Messaging;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Tests.Matching;
using Exparter.Application.Tests.Notifications;
using Exparter.Application.Notifications;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Common;
using Exparter.Domain.Messaging;

namespace Exparter.Application.Tests.Messaging;

internal sealed class InMemoryConversations : IConversationRepository
{
    public List<Conversation> Items { get; } = [];

    public List<Message> Messages { get; } = [];

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<Conversation?> FindByUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.IsParticipant(firstUserId) && x.IsParticipant(secondUserId)));

    public Task<IReadOnlyList<Conversation>> ListOpenOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Conversation>>(Items.Where(x => !x.IsClosed && x.IsParticipant(userId)).ToList());

    public Task<PagedList<Conversation>> ListAsync(Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        var all = Items.Where(x => x.IsParticipant(userId)).OrderByDescending(x => x.LastActivityAt).ToList();
        return Task.FromResult(new PagedList<Conversation>(all, page.Page, page.PageSize, all.Count));
    }

    public Task<IReadOnlyDictionary<Guid, Guid>> FindIdsByOtherUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(Items
            .Where(x => x.IsParticipant(userId) && otherUserIds.Contains(x.OtherThan(userId)))
            .ToDictionary(x => x.OtherThan(userId), x => x.Id));

    public Task<IReadOnlyDictionary<Guid, int>> CountUnreadAsync(
        Guid userId,
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(Items
            .Where(x => conversationIds.Contains(x.Id))
            .ToDictionary(
                x => x.Id,
                x => Messages.Count(m => m.ConversationId == x.Id
                    && m.SenderId != userId
                    && (x.LastReadAtOf(userId) is not { } readAt || m.SentAt > readAt))));

    public Task<IReadOnlyList<Message>> ListMessagesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Message>>(Messages.Where(x => ids.Contains(x.Id)).ToList());

    public Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid conversationId,
        MessagePosition? before,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Message>>(Messages
            .Where(x => x.ConversationId == conversationId
                && (before is null || x.SentAt < before.SentAt || (x.SentAt == before.SentAt && x.Id.CompareTo(before.Id) < 0)))
            .OrderByDescending(x => x.SentAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToList());

    public Task<Message?> FindMessageByClientIdAsync(
        Guid conversationId,
        Guid senderId,
        Guid clientMessageId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Messages.FirstOrDefault(
            x => x.ConversationId == conversationId && x.SenderId == senderId && x.ClientMessageId == clientMessageId));

    public void Add(Conversation conversation) => Items.Add(conversation);

    public void Add(Message message) => Messages.Add(message);
}

internal sealed class FakeMatchDirectory : IMatchDirectory
{
    public HashSet<Guid> Matched { get; } = [];

    public Task<MatchInfo?> FindAsync(Guid matchId, CancellationToken cancellationToken) => Task.FromResult<MatchInfo?>(null);

    public Task<IReadOnlyList<MatchSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MatchSummary>>([]);

    public Task<IReadOnlySet<Guid>> GetUsersWithOpenMatchAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(Matched.Where(otherUserIds.Contains).ToHashSet());
}

internal sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public List<(Guid UserId, string Event, object Payload)> Sent { get; } = [];

    public Task SendAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken)
    {
        Sent.Add((userId, eventName, payload));
        return Task.CompletedTask;
    }
}

public class MessagingHandlerTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly InMemoryConversations _conversations = new();
    private readonly FakeUserDirectory _users = new();
    private readonly FakeMatchDirectory _matches = new();
    private readonly RecordingRealtimeNotifier _realtime = new();
    private readonly FakePresence _presence = new();
    private readonly RecordingPushQueue _pushQueue = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();
    private readonly ConversationDtoFactory _dtos;
    private readonly Conversation _conversation;

    public MessagingHandlerTests()
    {
        _users.Users[_ali] = new UserInfo(_ali, "ali", IsActive: true, IsEmailVerified: true);
        _users.Users[_ayse] = new UserInfo(_ayse, "ayse", IsActive: true, IsEmailVerified: true);
        _dtos = new ConversationDtoFactory(_conversations, new FakeUserSummaries(_users), _matches);
        _conversation = Conversation.Open(_ali, _ayse, _clock.GetUtcNow());
        _conversations.Add(_conversation);
    }

    private PushNotifier Pushes => new(_presence, _pushQueue, _clock);

    private async Task<MessageDto> SendAsync(Guid userId, string body, Guid? clientMessageId = null)
    {
        _clock.Advance(TimeSpan.FromSeconds(1));
        return await new SendMessageCommandHandler(_conversations, _realtime, Pushes, _unitOfWork, _clock)
            .Handle(new SendMessageCommand(userId, _conversation.Id, body, clientMessageId), CancellationToken.None);
    }

    private Task<ConversationDto> GetAsync(Guid userId) =>
        new GetConversationQueryHandler(_conversations, _dtos)
            .Handle(new GetConversationQuery(userId, _conversation.Id), CancellationToken.None).AsTask();

    private Task<CursorPage<MessageDto>> ListMessagesAsync(Guid userId, string? cursor = null, int? limit = null) =>
        new ListMessagesQueryHandler(_conversations)
            .Handle(new ListMessagesQuery(userId, _conversation.Id, cursor, limit), CancellationToken.None).AsTask();

    private Task<ConversationDto> CloseAsync(Guid userId) =>
        new CloseConversationCommandHandler(_conversations, _matches, _dtos, _realtime, _unitOfWork, _clock)
            .Handle(new CloseConversationCommand(userId, _conversation.Id), CancellationToken.None).AsTask();

    private static async Task<string> AppCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<AppException>(action)).Code;

    [Fact]
    public async Task A_message_reaches_the_phone_of_a_recipient_without_an_open_app()
    {
        _presence.Connected.Add(_ali);
        await SendAsync(_ali, "Merhaba");
        await SendAsync(_ayse, "Selam");

        var (userId, message) = Assert.Single(_pushQueue.Queued);
        Assert.Equal((_ayse, "Exparter", "Yeni bir mesajınız var."), (userId, message.Title, message.Body));
        Assert.Equal(_conversation.Id.ToString(), message.Data["conversationId"]);
    }

    [Fact]
    public async Task A_new_match_opens_the_pairs_conversation_once()
    {
        var conversations = new InMemoryConversations();
        var handler = new OpenConversationOnMatchCreated(conversations, _clock);

        await handler.Handle(new MatchCreated(Guid.NewGuid(), _ali, _ayse), CancellationToken.None);
        await handler.Handle(new MatchCreated(Guid.NewGuid(), _ayse, _ali), CancellationToken.None);

        var conversation = Assert.Single(conversations.Items);
        Assert.True(conversation.IsParticipant(_ali));
        Assert.True(conversation.IsParticipant(_ayse));
    }

    [Fact]
    public async Task Matching_again_reopens_a_closed_conversation_with_its_history()
    {
        await SendAsync(_ali, "Merhaba");
        await CloseAsync(_ayse);

        await new OpenConversationOnMatchCreated(_conversations, _clock)
            .Handle(new MatchCreated(Guid.NewGuid(), _ayse, _ali), CancellationToken.None);

        Assert.False(Assert.Single(_conversations.Items).IsClosed);
        Assert.Single((await ListMessagesAsync(_ali)).Items);
    }

    [Fact]
    public async Task Send_stores_the_message_and_tells_both_users()
    {
        var clientId = Guid.NewGuid();

        var message = await SendAsync(_ali, " Merhaba ", clientId);

        Assert.Equal(("Merhaba", _ali, clientId), (message.Body, message.SenderId, message.ClientMessageId));
        Assert.Single(_conversations.Messages);
        Assert.Equal(
            [(_ayse, RealtimeEvents.MessageReceived), (_ali, RealtimeEvents.MessageReceived)],
            _realtime.Sent.Select(sent => (sent.UserId, sent.Event)));
        Assert.All(_realtime.Sent, sent => Assert.Equal(message, sent.Payload));
    }

    [Fact]
    public async Task Sending_the_same_client_id_again_returns_the_stored_message()
    {
        var clientId = Guid.NewGuid();
        var first = await SendAsync(_ali, "Merhaba", clientId);
        _realtime.Sent.Clear();

        var second = await SendAsync(_ali, "Merhaba", clientId);

        Assert.Equal(first, second);
        Assert.Single(_conversations.Messages);
        Assert.Empty(_realtime.Sent);
    }

    [Fact]
    public async Task The_same_client_id_from_the_other_user_is_a_different_message()
    {
        var clientId = Guid.NewGuid();
        await SendAsync(_ali, "Merhaba", clientId);

        await SendAsync(_ayse, "Selam", clientId);

        Assert.Equal(2, _conversations.Messages.Count);
    }

    [Fact]
    public async Task A_conversation_does_not_exist_for_anyone_but_its_two_users()
    {
        var stranger = Guid.NewGuid();

        Assert.Equal("not_found", await AppCodeOfAsync(() => SendAsync(stranger, "Merhaba")));
        Assert.Equal("not_found", await AppCodeOfAsync(() => GetAsync(stranger)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => ListMessagesAsync(stranger)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => CloseAsync(stranger)));
        Assert.Equal(
            "not_found",
            await AppCodeOfAsync(() => new MarkConversationReadCommandHandler(_conversations, _realtime, _unitOfWork, _clock)
                .Handle(new MarkConversationReadCommand(stranger, _conversation.Id), CancellationToken.None).AsTask()));
        Assert.Empty(_conversations.Messages);
    }

    [Fact]
    public async Task Each_user_sees_the_other_user_the_last_message_and_their_own_unread_count()
    {
        await SendAsync(_ali, "Bir");
        var last = await SendAsync(_ali, "İki");

        var forAyse = await GetAsync(_ayse);
        var forAli = await GetAsync(_ali);

        Assert.Equal(("ali", 2), (forAyse.OtherUser.Username, forAyse.UnreadCount));
        Assert.Equal(("ayse", 0), (forAli.OtherUser.Username, forAli.UnreadCount));
        Assert.Equal(last, forAyse.LastMessage);
        Assert.Null(forAli.OtherUserLastReadAt);
        Assert.NotNull(forAyse.OtherUserLastReadAt);
    }

    [Fact]
    public async Task Marking_read_clears_the_unread_count_and_lets_the_sender_see_it()
    {
        await SendAsync(_ali, "Merhaba");
        _clock.Advance(TimeSpan.FromMinutes(1));
        _realtime.Sent.Clear();

        await new MarkConversationReadCommandHandler(_conversations, _realtime, _unitOfWork, _clock)
            .Handle(new MarkConversationReadCommand(_ayse, _conversation.Id), CancellationToken.None);

        Assert.Equal(0, (await GetAsync(_ayse)).UnreadCount);
        Assert.Equal(_clock.GetUtcNow(), (await GetAsync(_ali)).OtherUserLastReadAt);
        var read = new ConversationReadDto(_conversation.Id, _ayse, _clock.GetUtcNow());
        Assert.Equal(
            [(_ali, RealtimeEvents.ConversationRead, (object)read), (_ayse, RealtimeEvents.ConversationRead, read)],
            _realtime.Sent);

        // A message that arrives afterwards is unread again.
        await SendAsync(_ali, "Orada mısınız?");
        Assert.Equal(1, (await GetAsync(_ayse)).UnreadCount);
    }

    [Fact]
    public async Task Messages_are_paged_from_newest_to_oldest_through_the_cursor()
    {
        for (var index = 1; index <= 5; index++)
        {
            await SendAsync(index % 2 == 0 ? _ali : _ayse, $"Mesaj {index}");
        }

        var first = await ListMessagesAsync(_ali, limit: 2);
        var second = await ListMessagesAsync(_ali, first.NextCursor, 2);
        var third = await ListMessagesAsync(_ali, second.NextCursor, 2);

        Assert.Equal(
            ["Mesaj 5", "Mesaj 4", "Mesaj 3", "Mesaj 2", "Mesaj 1"],
            first.Items.Concat(second.Items).Concat(third.Items).Select(message => message.Body));
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task Messages_sent_at_the_same_instant_are_not_lost_between_pages()
    {
        var handler = new SendMessageCommandHandler(_conversations, _realtime, Pushes, _unitOfWork, _clock);
        for (var index = 0; index < 4; index++)
        {
            await handler.Handle(new SendMessageCommand(_ali, _conversation.Id, $"Mesaj {index}", null), CancellationToken.None);
        }

        var first = await ListMessagesAsync(_ali, limit: 3);
        var second = await ListMessagesAsync(_ali, first.NextCursor, 3);

        Assert.Equal(4, first.Items.Concat(second.Items).Select(message => message.Id).Distinct().Count());
    }

    [Fact]
    public void Message_list_validator_rejects_out_of_range_limits_and_foreign_cursors()
    {
        var validator = new ListMessagesQueryValidator();

        Assert.True(validator.Validate(new ListMessagesQuery(_ali, _conversation.Id, null, 50)).IsValid);
        Assert.False(validator.Validate(new ListMessagesQuery(_ali, _conversation.Id, null, 0)).IsValid);
        Assert.False(validator.Validate(new ListMessagesQuery(_ali, _conversation.Id, null, 51)).IsValid);
        Assert.False(validator.Validate(new ListMessagesQuery(_ali, _conversation.Id, "not-a-cursor", null)).IsValid);
    }

    [Fact]
    public void Send_validator_requires_text_within_the_limit()
    {
        var validator = new SendMessageCommandValidator();

        Assert.True(validator.Validate(new SendMessageCommand(_ali, _conversation.Id, new string('a', 2000), null)).IsValid);
        Assert.False(validator.Validate(new SendMessageCommand(_ali, _conversation.Id, new string('a', 2001), null)).IsValid);
        Assert.False(validator.Validate(new SendMessageCommand(_ali, _conversation.Id, string.Empty, null)).IsValid);
    }

    [Fact]
    public async Task A_conversation_cannot_be_closed_while_the_pair_is_matched()
    {
        _matches.Matched.Add(_ayse);

        var exception = await Assert.ThrowsAsync<DomainException>(() => CloseAsync(_ali));

        Assert.Equal(MessagingErrors.ConversationHasActiveMatchCode, exception.Code);
        Assert.True((await GetAsync(_ali)).HasActiveMatch);
        Assert.Empty(_realtime.Sent);
    }

    [Fact]
    public async Task Closing_makes_the_conversation_read_only_and_tells_both_users()
    {
        await SendAsync(_ali, "Merhaba");
        _realtime.Sent.Clear();

        var closed = await CloseAsync(_ayse);

        Assert.True(closed.IsClosed);
        Assert.Equal(_ayse, closed.ClosedByUserId);
        Assert.Equal(
            [RealtimeEvents.ConversationUpdated, RealtimeEvents.ConversationUpdated],
            _realtime.Sent.Select(sent => sent.Event));
        // Each user is sent the conversation as they see it.
        Assert.Equal(
            ["ayse", "ali"],
            _realtime.Sent.OrderBy(sent => sent.UserId == _ali ? 0 : 1).Select(sent => ((ConversationDto)sent.Payload).OtherUser.Username));
        var exception = await Assert.ThrowsAsync<DomainException>(() => SendAsync(_ali, "Orada mısınız?"));
        Assert.Equal(MessagingErrors.ConversationClosedCode, exception.Code);
        Assert.Single((await ListMessagesAsync(_ayse)).Items);
    }

    [Fact]
    public async Task Conversations_are_listed_most_recently_active_first()
    {
        var carol = Guid.NewGuid();
        _users.Users[carol] = new UserInfo(carol, "carol", IsActive: true, IsEmailVerified: true);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _conversations.Add(Conversation.Open(_ali, carol, _clock.GetUtcNow()));
        var handler = new ListConversationsQueryHandler(_conversations, _dtos);

        var before = await handler.Handle(new ListConversationsQuery(_ali, null, null), CancellationToken.None);
        await SendAsync(_ayse, "Merhaba");
        var after = await handler.Handle(new ListConversationsQuery(_ali, null, null), CancellationToken.None);
        var forCarol = await handler.Handle(new ListConversationsQuery(carol, null, null), CancellationToken.None);

        Assert.Equal(["carol", "ayse"], before.Items.Select(item => item.OtherUser.Username));
        Assert.Equal(["ayse", "carol"], after.Items.Select(item => item.OtherUser.Username));
        Assert.Equal(1, after.Items[0].UnreadCount);
        Assert.Equal("ali", Assert.Single(forCarol.Items).OtherUser.Username);
    }
}
