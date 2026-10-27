using Exparter.Application.Common;
using Exparter.Domain.Notifications;
using Mediator;

namespace Exparter.Application.Notifications;

/// <summary>
/// A notification shown by the phone itself. The text is composed here rather than by
/// the app: the phone shows it even when the app is not running. It passes through
/// Google (FCM), outside Turkey, so it names no one and quotes nothing a user wrote
/// (docs/legal/compliance-research.md); the app shows the details once opened.
/// </summary>
/// <param name="Data">Tells the app which screen to open when the user taps it (docs/realtime.md).</param>
public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

/// <summary>Whether a user has an app open right now, connected to the real-time hub.</summary>
public interface IUserPresence
{
    bool IsConnected(Guid userId);
}

/// <summary>Hands push messages to a background sender, so that no request waits for FCM.</summary>
public interface IPushQueue
{
    void Enqueue(Guid userId, PushMessage message);
}

public interface IPushSender
{
    /// <returns>The tokens that no longer belong to an installation.</returns>
    Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken);
}

/// <summary>
/// Decides what reaches a user's phone: only users without an open app get a push, since
/// an open app already shows the change.
/// </summary>
public sealed class PushNotifier(IUserPresence presence, IPushQueue queue, TimeProvider timeProvider)
{
    public const string AppName = "Exparter";
    public const string MessageReceivedType = "MessageReceived";
    public const string MessageReceivedText = "Yeni bir mesajınız var.";

    public void Notify(IReadOnlyCollection<Notification> notifications)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var notification in notifications.Where(notification => !presence.IsConnected(notification.UserId)))
        {
            queue.Enqueue(
                notification.UserId,
                new PushMessage(AppName, PushTexts.Of(notification.Type, notification.Data, now), DataOf(notification)));
        }
    }

    /// <summary>New messages are not notifications; the phone only says that one arrived.</summary>
    public void Message(Guid recipientId, Guid conversationId)
    {
        if (presence.IsConnected(recipientId))
        {
            return;
        }

        queue.Enqueue(
            recipientId,
            new PushMessage(
                AppName,
                MessageReceivedText,
                new Dictionary<string, string> { ["type"] = MessageReceivedType, ["conversationId"] = conversationId.ToString() }));
    }

    private static Dictionary<string, string> DataOf(Notification notification)
    {
        var data = new Dictionary<string, string>
        {
            ["type"] = notification.Type.ToString(),
            ["notificationId"] = notification.Id.ToString(),
        };
        void Add(string key, Guid? value)
        {
            if (value is { } id)
            {
                data[key] = id.ToString();
            }
        }

        Add("swapRequestId", notification.SwapRequestId);
        Add("matchId", notification.MatchId);
        Add("sessionId", notification.SessionId);
        Add("reviewId", notification.ReviewId);
        Add("skillId", notification.SkillId);
        return data;
    }
}

/// <summary>
/// The sentences of the app's notification list (lib/l10n/app_tr.arb), for phones, with
/// "Bir kullanıcı" in place of the name.
/// </summary>
internal static class PushTexts
{
    private const string Someone = "Bir kullanıcı";

    public static string Of(NotificationType type, NotificationData data, DateTimeOffset now)
    {
        // A notification without an actor happened on its own, when a deadline passed.
        var byDeadline = data.ActorUserId is null;
        return type switch
        {
            NotificationType.SwapRequestReceived => $"{Someone} size takas isteği gönderdi.",
            NotificationType.SwapRequestAccepted => $"{Someone} takas isteğinizi kabul etti. Artık eşleştiniz.",
            NotificationType.SwapRequestDeclined => $"{Someone} takas isteğinizi reddetti.",
            NotificationType.SwapRequestCancelled => $"{Someone} takas isteğini geri çekti.",
            NotificationType.SwapRequestExpired => "Takas isteğiniz cevaplanmadığı için süresi doldu.",
            NotificationType.MatchEndRequested => $"{Someone} eşleşmeyi sonlandırmak istiyor.",
            NotificationType.MatchEndRequestRejected => $"{Someone} sonlandırma isteğinizi reddetti; eşleşme sürüyor.",
            NotificationType.MatchEndRequestWithdrawn => $"{Someone} sonlandırma isteğini geri çekti.",
            NotificationType.MatchEndReminder =>
                $"{Someone} eşleşmeyi sonlandırmak istiyor. Cevap vermezseniz eşleşme yakında sona erecek.",
            NotificationType.MatchEnded when byDeadline => "Sonlandırma isteği cevaplanmadığı için bir eşleşmeniz sona erdi.",
            NotificationType.MatchEnded => "Bir eşleşmeniz sona erdi.",
            NotificationType.SessionProposed => $"{Someone} bir çevrimiçi oturum önerdi.",
            NotificationType.SessionAccepted => $"{Someone} oturum önerinizi kabul etti.",
            NotificationType.SessionDeclined when byDeadline => "Oturum öneriniz cevaplanmadığı için geçersiz oldu.",
            NotificationType.SessionDeclined => $"{Someone} oturum önerinizi reddetti.",
            NotificationType.SessionCancelled => $"{Someone} oturumu iptal etti.",

            // The server does not know the phone's time zone, so it says how far away the start is.
            NotificationType.SessionStartingSoon when data.Deadline is { } start && start - now > TimeSpan.FromHours(2) =>
                "Çevrimiçi oturumunuz yarın bu saatte başlıyor.",
            NotificationType.SessionStartingSoon => "Çevrimiçi oturumunuz yaklaşık bir saat sonra başlıyor.",
            NotificationType.SessionCompletionMarked => $"{Someone} oturumu tamamlandı olarak işaretledi. Onayınız bekleniyor.",
            NotificationType.SessionCompletionReminder =>
                "Tamamlandı işaretine cevap vermezseniz oturum yakında tamamlanmış sayılacak.",
            NotificationType.SessionCompleted when byDeadline => "Bir oturumunuz, süre dolduğu için tamamlanmış sayıldı.",
            NotificationType.SessionCompleted => $"{Someone} oturumun tamamlandığını onayladı.",
            NotificationType.ReviewReceived => $"{Someone} sizi değerlendirdi.",
            NotificationType.SkillProposalApproved => "Önerdiğiniz yetenek onaylandı.",
            NotificationType.SkillProposalRejected => "Önerdiğiniz yetenek reddedildi.",
            NotificationType.ReportReviewed => "Şikâyetiniz incelendi. Bildirdiğiniz için teşekkür ederiz.",
            _ => "Yeni bir bildiriminiz var.",
        };
    }
}

/// <summary>Sends a push message to every device of a user, and forgets the devices FCM no longer knows.</summary>
public sealed record SendPushCommand(Guid UserId, PushMessage Message) : ICommand;

public sealed class SendPushCommandHandler(IDeviceRepository devices, IPushSender sender) : ICommandHandler<SendPushCommand>
{
    public async ValueTask<Unit> Handle(SendPushCommand command, CancellationToken cancellationToken)
    {
        var tokens = await devices.ListTokensAsync(command.UserId, cancellationToken);
        if (tokens.Count == 0)
        {
            return Unit.Value;
        }

        var stale = await sender.SendAsync(tokens, command.Message, cancellationToken);
        if (stale.Count > 0)
        {
            await devices.DeleteByTokensAsync(stale, cancellationToken);
        }

        return Unit.Value;
    }
}
