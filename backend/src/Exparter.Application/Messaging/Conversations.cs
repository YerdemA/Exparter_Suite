using Exparter.Application.Identity;
using System.Globalization;
using System.Text;
using Exparter.Application.Common;
using Exparter.Application.Matching;
using Exparter.Application.Moderation;
using Exparter.Application.Notifications;
using Exparter.Domain.Messaging;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Messaging;

/// <summary>A position in a conversation's messages: everything older than this message.</summary>
public sealed record MessagePosition(DateTimeOffset SentAt, Guid Id);

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Conversation?> FindByUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);

    /// <summary>The user's conversations, most recently active first.</summary>
    Task<PagedList<Conversation>> ListAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The user's conversations that are not closed.</summary>
    Task<IReadOnlyList<Conversation>> ListOpenOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The conversations that exist with any of the other users, keyed by that other user.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> FindIdsByOtherUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken);

    /// <summary>Per conversation, how many messages from the other user came after the user last read it.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountUnreadAsync(
        Guid userId,
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Message>> ListMessagesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Newest first, starting just before <paramref name="before"/> or at the newest message.</summary>
    Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid conversationId,
        MessagePosition? before,
        int limit,
        CancellationToken cancellationToken);

    Task<Message?> FindMessageByClientIdAsync(
        Guid conversationId,
        Guid senderId,
        Guid clientMessageId,
        CancellationToken cancellationToken);

    void Add(Conversation conversation);

    void Add(Message message);
}

/// <summary>What other modules may know about conversations.</summary>
/// <summary>A message as a report sees it.</summary>
public sealed record MessageInfo(Guid Id, Guid SenderId, string Body);

public interface IConversationDirectory
{
    /// <summary>A message, if <paramref name="userId"/> is one of the two users of its conversation.</summary>
    Task<MessageInfo?> FindMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IConversationRepository.FindIdsByOtherUserAsync"/>
    Task<IReadOnlyDictionary<Guid, Guid>> FindIdsByOtherUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken);
}

internal sealed class ConversationDirectory(IConversationRepository conversations) : IConversationDirectory
{
    public async Task<MessageInfo?> FindMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken)
    {
        if ((await conversations.ListMessagesByIdsAsync([messageId], cancellationToken)).SingleOrDefault() is not { } message
            || await conversations.GetByIdAsync(message.ConversationId, cancellationToken) is not { } conversation
            || !conversation.IsParticipant(userId))
        {
            return null;
        }

        return new MessageInfo(message.Id, message.SenderId, message.Body);
    }

    public Task<IReadOnlyDictionary<Guid, Guid>> FindIdsByOtherUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken) =>
        conversations.FindIdsByOtherUserAsync(userId, otherUserIds, cancellationToken);
}

public sealed record MessageDto(Guid Id, Guid ConversationId, Guid SenderId, string Body, DateTimeOffset SentAt, Guid? ClientMessageId)
{
    public static MessageDto From(Message message) =>
        new(message.Id, message.ConversationId, message.SenderId, message.Body, message.SentAt, message.ClientMessageId);
}

/// <summary>A conversation as one of its two users sees it.</summary>
/// <param name="OtherUserLastReadAt">When the other user last read the conversation; null if never.</param>
public sealed record ConversationDto(
    Guid Id,
    UserSummaryDto OtherUser,
    bool IsClosed,
    DateTimeOffset? ClosedAt,
    Guid? ClosedByUserId,
    bool HasActiveMatch,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTimeOffset? OtherUserLastReadAt,
    DateTimeOffset CreatedAt);

public sealed record ConversationReadDto(Guid ConversationId, Guid UserId, DateTimeOffset ReadAt);

/// <summary>Turns conversations into what the API returns for a given viewer.</summary>
public sealed class ConversationDtoFactory(
    IConversationRepository conversations,
    IUserSummaries users,
    IMatchDirectory matches)
{
    public async Task<ConversationDto> ToDtoAsync(Conversation conversation, Guid viewerId, CancellationToken cancellationToken) =>
        (await ToDtosAsync([conversation], viewerId, cancellationToken))[0];

    public async Task<IReadOnlyList<ConversationDto>> ToDtosAsync(
        IReadOnlyList<Conversation> items,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        var others = items.Select(item => item.OtherThan(viewerId)).ToList();
        var summaries = await users.GetAsync(others, cancellationToken);
        var matched = await matches.GetUsersWithOpenMatchAsync(viewerId, others, cancellationToken);
        var unread = await conversations.CountUnreadAsync(viewerId, items.Select(item => item.Id).ToList(), cancellationToken);
        var lastMessages = (await conversations.ListMessagesByIdsAsync(
                items.Where(item => item.LastMessageId is not null).Select(item => item.LastMessageId!.Value).ToList(),
                cancellationToken))
            .ToDictionary(message => message.Id);

        return items.Select(item =>
            {
                var other = item.OtherThan(viewerId);
                return new ConversationDto(
                    item.Id,
                    summaries[other],
                    item.IsClosed,
                    item.ClosedAt,
                    item.ClosedByUserId,
                    matched.Contains(other),
                    item.LastMessageId is { } lastId && lastMessages.TryGetValue(lastId, out var last) ? MessageDto.From(last) : null,
                    unread.GetValueOrDefault(item.Id),
                    item.LastReadAtOf(other),
                    item.CreatedAt);
            })
            .ToList();
    }
}

internal static class MessagingExtensions
{
    /// <summary>
    /// A conversation is visible only to its two users; to anyone else it does not exist,
    /// so that ids cannot be probed.
    /// </summary>
    public static async Task<Conversation> GetForParticipantAsync(
        this IConversationRepository conversations,
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await conversations.GetByIdAsync(conversationId, cancellationToken) is { } conversation && conversation.IsParticipant(userId)
            ? conversation
            : throw AppException.NotFound("The conversation was not found.");

    /// <summary>Each of the two users gets the conversation as they see it.</summary>
    public static async Task SendConversationUpdatedAsync(
        this IRealtimeNotifier realtime,
        ConversationDtoFactory dtos,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        foreach (var userId in new[] { conversation.UserLowId, conversation.UserHighId })
        {
            await realtime.SendAsync(
                userId,
                RealtimeEvents.ConversationUpdated,
                await dtos.ToDtoAsync(conversation, userId, cancellationToken),
                cancellationToken);
        }
    }
}

/// <summary>Opens the conversation of a new match, or reopens the one the pair already has, in the match's own transaction.</summary>
public sealed class OpenConversationOnMatchCreated(IConversationRepository conversations, TimeProvider timeProvider)
    : INotificationHandler<MatchCreated>
{
    public async ValueTask Handle(MatchCreated notification, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var conversation = await conversations.FindByUsersAsync(notification.UserAId, notification.UserBId, cancellationToken);
        if (conversation is null)
        {
            conversations.Add(Conversation.Open(notification.UserAId, notification.UserBId, now));
        }
        else if (conversation.IsClosed)
        {
            conversation.Reopen(now);
        }
    }
}

public sealed record ListConversationsQuery(Guid UserId, int? Page, int? PageSize) : IQuery<PagedList<ConversationDto>>;

public sealed class ListConversationsQueryHandler(IConversationRepository conversations, ConversationDtoFactory dtos)
    : IQueryHandler<ListConversationsQuery, PagedList<ConversationDto>>
{
    public async ValueTask<PagedList<ConversationDto>> Handle(ListConversationsQuery query, CancellationToken cancellationToken)
    {
        var page = await conversations.ListAsync(query.UserId, PageRequest.From(query.Page, query.PageSize), cancellationToken);
        return new PagedList<ConversationDto>(
            await dtos.ToDtosAsync(page.Items, query.UserId, cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}

public sealed record GetConversationQuery(Guid UserId, Guid ConversationId) : IQuery<ConversationDto>;

public sealed class GetConversationQueryHandler(IConversationRepository conversations, ConversationDtoFactory dtos)
    : IQueryHandler<GetConversationQuery, ConversationDto>
{
    public async ValueTask<ConversationDto> Handle(GetConversationQuery query, CancellationToken cancellationToken) =>
        await dtos.ToDtoAsync(
            await conversations.GetForParticipantAsync(query.ConversationId, query.UserId, cancellationToken),
            query.UserId,
            cancellationToken);
}

public sealed record ListMessagesQuery(Guid UserId, Guid ConversationId, string? Cursor, int? Limit) : IQuery<CursorPage<MessageDto>>;

public sealed class ListMessagesQueryValidator : AbstractValidator<ListMessagesQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public ListMessagesQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit);
        RuleFor(x => x.Cursor)
            .Must(cursor => cursor is null || MessageCursor.TryDecode(cursor, out _))
            .WithMessage("The cursor is not one returned by this endpoint.");
    }
}

/// <summary>The cursor names the oldest message of the previous page; it is opaque to clients.</summary>
internal static class MessageCursor
{
    public static string Encode(MessagePosition position) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes(
                string.Create(CultureInfo.InvariantCulture, $"{position.SentAt.UtcTicks}:{position.Id:N}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string cursor, out MessagePosition? position)
    {
        position = null;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            var parts = Encoding.ASCII.GetString(Convert.FromBase64String(padded)).Split(':');
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || ticks > DateTimeOffset.MaxValue.UtcTicks
                || !Guid.TryParseExact(parts[1], "N", out var id))
            {
                return false;
            }

            position = new MessagePosition(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class ListMessagesQueryHandler(IConversationRepository conversations)
    : IQueryHandler<ListMessagesQuery, CursorPage<MessageDto>>
{
    public async ValueTask<CursorPage<MessageDto>> Handle(ListMessagesQuery query, CancellationToken cancellationToken)
    {
        await conversations.GetForParticipantAsync(query.ConversationId, query.UserId, cancellationToken);
        var limit = query.Limit ?? ListMessagesQueryValidator.DefaultLimit;
        MessagePosition? before = null;
        if (query.Cursor is not null)
        {
            MessageCursor.TryDecode(query.Cursor, out before);
        }

        // One more than asked for: its presence says whether there is an older page.
        var messages = await conversations.ListMessagesAsync(query.ConversationId, before, limit + 1, cancellationToken);
        var items = messages.Take(limit).ToList();
        return new CursorPage<MessageDto>(
            items.Select(MessageDto.From).ToList(),
            messages.Count > limit ? MessageCursor.Encode(new MessagePosition(items[^1].SentAt, items[^1].Id)) : null);
    }
}

public sealed record SendMessageCommand(Guid UserId, Guid ConversationId, string Body, Guid? ClientMessageId) : ICommand<MessageDto>;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(Message.BodyMaxLength);
    }
}

public sealed class SendMessageCommandHandler(
    IConversationRepository conversations,
    IRealtimeNotifier realtime,
    PushNotifier pushes,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SendMessageCommand, MessageDto>
{
    public async ValueTask<MessageDto> Handle(SendMessageCommand command, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetForParticipantAsync(command.ConversationId, command.UserId, cancellationToken);

        // A client that did not hear back sends the same message again with the same id;
        // it gets the stored message instead of a duplicate.
        if (command.ClientMessageId is { } clientMessageId
            && await conversations.FindMessageByClientIdAsync(conversation.Id, command.UserId, clientMessageId, cancellationToken)
                is { } existing)
        {
            return MessageDto.From(existing);
        }

        var message = conversation.Post(command.UserId, command.Body, command.ClientMessageId, timeProvider.GetUtcNow());
        conversations.Add(message);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = MessageDto.From(message);

        // The sender is told too: their other devices show the message as well.
        await realtime.SendAsync(conversation.OtherThan(command.UserId), RealtimeEvents.MessageReceived, dto, cancellationToken);
        await realtime.SendAsync(command.UserId, RealtimeEvents.MessageReceived, dto, cancellationToken);
        await pushes.MessageAsync(conversation.OtherThan(command.UserId), command.UserId, conversation.Id, message.Body, cancellationToken);
        return dto;
    }
}

public sealed record MarkConversationReadCommand(Guid UserId, Guid ConversationId) : ICommand;

public sealed class MarkConversationReadCommandHandler(
    IConversationRepository conversations,
    IRealtimeNotifier realtime,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<MarkConversationReadCommand>
{
    public async ValueTask<Unit> Handle(MarkConversationReadCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var conversation = await conversations.GetForParticipantAsync(command.ConversationId, command.UserId, cancellationToken);
        conversation.MarkRead(command.UserId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var read = new ConversationReadDto(conversation.Id, command.UserId, now);
        await realtime.SendAsync(conversation.OtherThan(command.UserId), RealtimeEvents.ConversationRead, read, cancellationToken);
        await realtime.SendAsync(command.UserId, RealtimeEvents.ConversationRead, read, cancellationToken);
        return Unit.Value;
    }
}

public sealed record CloseConversationCommand(Guid UserId, Guid ConversationId) : ICommand<ConversationDto>;

public sealed class CloseConversationCommandHandler(
    IConversationRepository conversations,
    IMatchDirectory matches,
    ConversationDtoFactory dtos,
    IRealtimeNotifier realtime,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CloseConversationCommand, ConversationDto>
{
    public async ValueTask<ConversationDto> Handle(CloseConversationCommand command, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetForParticipantAsync(command.ConversationId, command.UserId, cancellationToken);
        var other = conversation.OtherThan(command.UserId);
        var hasActiveMatch = (await matches.GetUsersWithOpenMatchAsync(command.UserId, [other], cancellationToken)).Contains(other);

        conversation.Close(command.UserId, hasActiveMatch, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await realtime.SendConversationUpdatedAsync(dtos, conversation, cancellationToken);
        return await dtos.ToDtoAsync(conversation, command.UserId, cancellationToken);
    }
}

/// <summary>A user is leaving: their conversations become read-only, in the transaction that records the deletion request.</summary>
public sealed class CloseConversationsOnAccountDeletion(IConversationRepository conversations, TimeProvider timeProvider)
    : INotificationHandler<AccountDeletionRequested>
{
    public async ValueTask Handle(AccountDeletionRequested notification, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var conversation in await conversations.ListOpenOfUserAsync(notification.UserId, cancellationToken))
        {
            conversation.CloseBecauseAccountDeleted(notification.UserId, now);
        }
    }
}

/// <summary>A block closes the conversation of the two users in the block's own transaction.</summary>
public sealed class CloseConversationOnBlock(IConversationRepository conversations, TimeProvider timeProvider)
    : INotificationHandler<UserBlocked>
{
    public async ValueTask Handle(UserBlocked notification, CancellationToken cancellationToken)
    {
        var conversation = await conversations.FindByUsersAsync(notification.BlockerId, notification.BlockedId, cancellationToken);
        conversation?.CloseBecauseBlocked(notification.BlockerId, timeProvider.GetUtcNow());
    }
}
