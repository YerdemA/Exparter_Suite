namespace Exparter.Domain.Notifications;

public enum NotificationType
{
    SwapRequestReceived,
    SwapRequestAccepted,
    SwapRequestDeclined,
    SwapRequestCancelled,
    SwapRequestExpired,
    MatchEndRequested,
    MatchEndRequestRejected,
    MatchEndRequestWithdrawn,
    MatchEndReminder,
    MatchEnded,
    SessionProposed,
    SessionAccepted,
    SessionDeclined,
    SessionCancelled,
    SessionStartingSoon,
    SessionCompletionMarked,
    SessionCompletionReminder,
    SessionCompleted,
    ReviewReceived,
    SkillProposalApproved,
    SkillProposalRejected,
}

/// <summary>
/// What a notification is about. Which fields are set depends on its type; the text
/// shown to the user is composed by the client from these.
/// </summary>
/// <param name="ActorUserId">The user whose action caused the notification, if any.</param>
/// <param name="Reason">The reason given for cancelling a session.</param>
/// <param name="Deadline">The moment something happens on its own: a deadline, or a session's start.</param>
public sealed record NotificationData(
    Guid? ActorUserId = null,
    Guid? SwapRequestId = null,
    Guid? MatchId = null,
    Guid? SessionId = null,
    Guid? ReviewId = null,
    Guid? SkillId = null,
    string? Reason = null,
    DateTimeOffset? Deadline = null);

/// <summary>Something that happened and that a user should know about.</summary>
public sealed class Notification
{
    /// <summary>How long a notification is kept before it is removed.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private Notification()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? SwapRequestId { get; private set; }
    public Guid? MatchId { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? ReviewId { get; private set; }
    public Guid? SkillId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public NotificationData Data => new(ActorUserId, SwapRequestId, MatchId, SessionId, ReviewId, SkillId, Reason, Deadline);

    public static Notification Create(Guid userId, NotificationType type, NotificationData data, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Type = type,
            ActorUserId = data.ActorUserId,
            SwapRequestId = data.SwapRequestId,
            MatchId = data.MatchId,
            SessionId = data.SessionId,
            ReviewId = data.ReviewId,
            SkillId = data.SkillId,
            Reason = data.Reason,
            Deadline = data.Deadline,
            CreatedAt = now,
        };

    /// <summary>The first reading stands; reading again changes nothing.</summary>
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}

public enum DevicePlatform
{
    Android,
    Ios,
    Web,
}

/// <summary>An installation of a client that push notifications can be sent to.</summary>
public sealed class Device
{
    public const int TokenMaxLength = 4096;

    private Device()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string FcmToken { get; private set; } = null!;
    public DevicePlatform Platform { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public static Device Register(Guid userId, string fcmToken, DevicePlatform platform, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FcmToken = fcmToken,
            Platform = platform,
            CreatedAt = now,
            LastSeenAt = now,
        };

    /// <summary>
    /// The same installation registers again, possibly after another user signed in on
    /// it: from now on it belongs to that user, and nobody else's notifications reach it.
    /// </summary>
    public void Refresh(Guid userId, DevicePlatform platform, DateTimeOffset now)
    {
        UserId = userId;
        Platform = platform;
        LastSeenAt = now;
    }
}
