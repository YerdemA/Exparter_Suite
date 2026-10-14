using Exparter.Application.Identity;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Notifications;
using Exparter.Application.Tests.Matching;
using Exparter.Application.Tests.Messaging;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Notifications;

namespace Exparter.Application.Tests.Notifications;

internal sealed class FakePresence : IUserPresence
{
    public HashSet<Guid> Connected { get; } = [];

    public bool IsConnected(Guid userId) => Connected.Contains(userId);
}

internal sealed class RecordingPushQueue : IPushQueue
{
    public List<(Guid UserId, PushMessage Message)> Queued { get; } = [];

    public void Enqueue(Guid userId, PushMessage message) => Queued.Add((userId, message));
}

internal sealed class RecordingPushSender : IPushSender
{
    public HashSet<string> Stale { get; } = [];

    public List<(IReadOnlyCollection<string> Tokens, PushMessage Message)> Sent { get; } = [];

    public Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        Sent.Add((tokens, message));
        return Task.FromResult<IReadOnlyCollection<string>>(tokens.Where(Stale.Contains).ToList());
    }
}

public class PushTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly FakeUserDirectory _users = new();
    private readonly FakePresence _presence = new();
    private readonly RecordingPushQueue _queue = new();
    private readonly InMemoryNotifications _notifications = new();
    private readonly RecordingRealtimeNotifier _realtime = new();
    private readonly TestClock _clock = new();
    private readonly UserNotifier _notifier;
    private readonly PushNotifier _pushes;

    public PushTests()
    {
        _users.Users[_ali] = new UserInfo(_ali, "ali", IsActive: true, IsEmailVerified: true);
        _users.Users[_ayse] = new UserInfo(_ayse, "ayse", IsActive: true, IsEmailVerified: true);
        _pushes = new PushNotifier(_presence, _queue, new FakeUserSummaries(_users), _clock);
        _notifier = new UserNotifier(_notifications, _realtime, _pushes, _clock);
    }

    [Fact]
    public async Task Only_users_without_an_open_app_get_a_push_with_the_text_and_what_to_open()
    {
        var matchId = Guid.NewGuid();
        _presence.Connected.Add(_ali);

        _notifier.Notify(_ayse, NotificationType.SwapRequestAccepted, new NotificationData(ActorUserId: _ali, MatchId: matchId));
        _notifier.Notify(_ali, NotificationType.ReviewReceived, new NotificationData(ActorUserId: _ayse));
        await _notifier.DeliverAsync(CancellationToken.None);

        Assert.Equal(2, _realtime.Sent.Count);
        var (userId, message) = Assert.Single(_queue.Queued);
        Assert.Equal(_ayse, userId);
        Assert.Equal(("Exparter", "@ali takas isteğinizi kabul etti. Artık eşleştiniz."), (message.Title, message.Body));
        Assert.Equal("SwapRequestAccepted", message.Data["type"]);
        Assert.Equal(matchId.ToString(), message.Data["matchId"]);
        Assert.Equal(_notifications.Items[0].Id.ToString(), message.Data["notificationId"]);
        Assert.False(message.Data.ContainsKey("sessionId"));
    }

    [Fact]
    public async Task What_happened_on_its_own_or_by_a_deleted_user_is_told_without_a_name()
    {
        var gone = Guid.NewGuid();
        _notifier.Notify(_ayse, NotificationType.MatchEnded, new NotificationData(MatchId: Guid.NewGuid()));
        _notifier.Notify(_ayse, NotificationType.MatchEnded, new NotificationData(ActorUserId: gone, MatchId: Guid.NewGuid()));
        _notifier.Notify(
            _ayse,
            NotificationType.SessionCancelled,
            new NotificationData(ActorUserId: _ali, SessionId: Guid.NewGuid(), Reason: "Hastayım."));
        await _notifier.DeliverAsync(CancellationToken.None);

        Assert.Equal(
            [
                "Sonlandırma isteği cevaplanmadığı için bir eşleşmeniz sona erdi.",
                "Bir kullanıcı ile eşleşmeniz sona erdi.",
                "@ali oturumu iptal etti: Hastayım.",
            ],
            _queue.Queued.Select(x => x.Message.Body));
    }

    [Fact]
    public async Task A_session_reminder_says_how_far_away_the_start_is()
    {
        var now = _clock.GetUtcNow();
        _notifier.Notify(_ayse, NotificationType.SessionStartingSoon, new NotificationData(Deadline: now.AddHours(24)));
        _notifier.Notify(_ayse, NotificationType.SessionStartingSoon, new NotificationData(Deadline: now.AddHours(1)));
        await _notifier.DeliverAsync(CancellationToken.None);

        Assert.Equal(
            ["Çevrimiçi oturumunuz yarın bu saatte başlıyor.", "Çevrimiçi oturumunuz yaklaşık bir saat sonra başlıyor."],
            _queue.Queued.Select(x => x.Message.Body));
    }

    [Fact]
    public async Task A_message_push_shows_the_sender_and_how_the_message_starts()
    {
        var conversationId = Guid.NewGuid();

        await _pushes.MessageAsync(_ayse, _ali, conversationId, "Merhaba!", CancellationToken.None);
        await _pushes.MessageAsync(_ayse, _ali, conversationId, new string('a', 150), CancellationToken.None);
        _presence.Connected.Add(_ayse);
        await _pushes.MessageAsync(_ayse, _ali, conversationId, "Görüldü", CancellationToken.None);

        Assert.Equal(2, _queue.Queued.Count);
        var first = _queue.Queued[0].Message;
        Assert.Equal(("@ali", "Merhaba!"), (first.Title, first.Body));
        Assert.Equal("MessageReceived", first.Data["type"]);
        Assert.Equal(conversationId.ToString(), first.Data["conversationId"]);
        Assert.Equal(new string('a', 100) + "…", _queue.Queued[1].Message.Body);
    }

    [Fact]
    public async Task Asking_to_delete_an_account_stops_pushes_to_its_phones()
    {
        var devices = new InMemoryDevices();
        devices.Add(Device.Register(_ayse, "phone", DevicePlatform.Android, _clock.GetUtcNow()));
        devices.Add(Device.Register(_ali, "ali-phone", DevicePlatform.Android, _clock.GetUtcNow()));

        await new RemoveDevicesOnAccountDeletionRequested(devices)
            .Handle(new AccountDeletionRequested(_ayse), CancellationToken.None);

        Assert.Equal("ali-phone", Assert.Single(devices.Items).FcmToken);
    }

    [Fact]
    public async Task A_push_goes_to_every_device_of_the_user_and_forgets_the_ones_fcm_no_longer_knows()
    {
        var devices = new InMemoryDevices();
        var sender = new RecordingPushSender();
        var now = _clock.GetUtcNow();
        devices.Add(Device.Register(_ayse, "phone", DevicePlatform.Android, now));
        devices.Add(Device.Register(_ayse, "old-phone", DevicePlatform.Android, now));
        devices.Add(Device.Register(_ali, "ali-phone", DevicePlatform.Android, now));
        sender.Stale.Add("old-phone");
        var handler = new SendPushCommandHandler(devices, sender);
        var message = new PushMessage("Exparter", "Merhaba", new Dictionary<string, string>());

        await handler.Handle(new SendPushCommand(_ayse, message), CancellationToken.None);
        await handler.Handle(new SendPushCommand(Guid.NewGuid(), message), CancellationToken.None);

        var (tokens, sent) = Assert.Single(sender.Sent);
        Assert.Equal(["phone", "old-phone"], tokens);
        Assert.Same(message, sent);
        Assert.Equal(["phone", "ali-phone"], devices.Items.Select(x => x.FcmToken));
    }
}
