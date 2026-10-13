using Exparter.Domain.Common;

namespace Exparter.Domain.Messaging;

public static class MessagingErrors
{
    public const string ConversationClosedCode = "conversation_closed";
    public const string ConversationHasActiveMatchCode = "conversation_has_active_match";
    public const string InvalidMessageCode = "invalid_message";

    public static DomainException ConversationClosed() =>
        new(ConversationClosedCode, "The conversation is closed.");

    public static DomainException ConversationHasActiveMatch() =>
        new(ConversationHasActiveMatchCode, "A conversation cannot be closed while the two users have a match that has not ended.");

    public static DomainException InvalidMessage() =>
        new(InvalidMessageCode, $"A message must contain text and be at most {Message.BodyMaxLength} characters.");
}

/// <summary>
/// The one conversation between two users. It is opened by their first match and lives
/// on after the match ends; if they match again it continues with its history.
/// </summary>
public sealed class Conversation
{
    private Conversation()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The two users, stored smaller id first so that a pair maps to one row whichever
    /// of them comes first.
    /// </summary>
    public Guid UserLowId { get; private set; }

    public Guid UserHighId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the conversation was opened, reopened or last written to; what lists sort by.</summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    public Guid? LastMessageId { get; private set; }
    public DateTimeOffset? UserLowLastReadAt { get; private set; }
    public DateTimeOffset? UserHighLastReadAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }

    public bool IsClosed => ClosedAt is not null;

    public static (Guid Low, Guid High) Order(Guid firstUserId, Guid secondUserId) =>
        firstUserId.CompareTo(secondUserId) < 0 ? (firstUserId, secondUserId) : (secondUserId, firstUserId);

    public static Conversation Open(Guid firstUserId, Guid secondUserId, DateTimeOffset now)
    {
        var (low, high) = Order(firstUserId, secondUserId);
        return new Conversation
        {
            Id = Guid.CreateVersion7(),
            UserLowId = low,
            UserHighId = high,
            CreatedAt = now,
            LastActivityAt = now,
        };
    }

    public bool IsParticipant(Guid userId) => userId == UserLowId || userId == UserHighId;

    public Guid OtherThan(Guid userId) => userId == UserLowId ? UserHighId : UserLowId;

    public DateTimeOffset? LastReadAtOf(Guid userId) => userId == UserLowId ? UserLowLastReadAt : UserHighLastReadAt;

    /// <summary>A participant writes a message. The sender has thereby read everything before it.</summary>
    public Message Post(Guid senderId, string body, Guid? clientMessageId, DateTimeOffset now)
    {
        if (IsClosed)
        {
            throw MessagingErrors.ConversationClosed();
        }

        var message = Message.Create(Id, senderId, body, clientMessageId, now);
        LastMessageId = message.Id;
        LastActivityAt = now;
        SetLastReadAt(senderId, now);
        return message;
    }

    public void MarkRead(Guid userId, DateTimeOffset now) => SetLastReadAt(userId, now);

    /// <summary>Makes the conversation read-only. Closing a closed conversation changes nothing.</summary>
    /// <param name="hasActiveMatch">Whether the two users have a match that has not ended.</param>
    public void Close(Guid userId, bool hasActiveMatch, DateTimeOffset now)
    {
        if (IsClosed)
        {
            return;
        }

        if (hasActiveMatch)
        {
            throw MessagingErrors.ConversationHasActiveMatch();
        }

        ClosedAt = now;
        ClosedByUserId = userId;
    }

    /// <summary>The two users matched again: the conversation continues where it stopped.</summary>
    public void Reopen(DateTimeOffset now)
    {
        ClosedAt = null;
        ClosedByUserId = null;
        LastActivityAt = now;
    }

    private void SetLastReadAt(Guid userId, DateTimeOffset now)
    {
        if (userId == UserLowId)
        {
            UserLowLastReadAt = now;
        }
        else
        {
            UserHighLastReadAt = now;
        }
    }
}

public sealed class Message
{
    public const int BodyMaxLength = 2000;

    private Message()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = null!;

    /// <summary>Chosen by the sending client, so that a retried send is not stored twice.</summary>
    public Guid? ClientMessageId { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    internal static Message Create(Guid conversationId, Guid senderId, string body, Guid? clientMessageId, DateTimeOffset now)
    {
        var trimmed = body.Trim();
        if (trimmed.Length is 0 or > BodyMaxLength)
        {
            throw MessagingErrors.InvalidMessage();
        }

        return new Message
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            SenderId = senderId,
            Body = trimmed,
            ClientMessageId = clientMessageId,
            SentAt = now,
        };
    }
}
