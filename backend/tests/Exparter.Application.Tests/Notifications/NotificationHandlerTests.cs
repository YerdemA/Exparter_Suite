using Exparter.Application.Common;
using Exparter.Application.Notifications;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Tests.Messaging;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Notifications;

namespace Exparter.Application.Tests.Notifications;

internal sealed class InMemoryNotifications : INotificationRepository
{
    public List<Notification> Items { get; } = [];

    public Task<PagedList<Notification>> ListAsync(Guid userId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken)
    {
        var all = Items.Where(x => x.UserId == userId && (!unreadOnly || x.ReadAt is null)).OrderByDescending(x => x.CreatedAt).ToList();
        return Task.FromResult(new PagedList<Notification>(all, page.Page, page.PageSize, all.Count));
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(x => x.UserId == userId && x.ReadAt is null));

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Items.Where(x => x.UserId == userId).ToList().ForEach(x => x.MarkRead(now));
        return Task.CompletedTask;
    }

    public Task<int> DeleteCreatedBeforeAsync(DateTimeOffset moment, CancellationToken cancellationToken) =>
        Task.FromResult(Items.RemoveAll(x => x.CreatedAt < moment));

    public Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        Items.RemoveAll(x => x.UserId == userId);
        return Task.CompletedTask;
    }

    public void Add(Notification notification) => Items.Add(notification);
}

internal sealed class InMemoryDevices : IDeviceRepository
{
    public List<Device> Items { get; } = [];

    public Task<Device?> FindByTokenAsync(string fcmToken, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.FcmToken == fcmToken));

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task DeleteByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        Items.RemoveAll(x => x.UserId == userId);
        return Task.CompletedTask;
    }

    public void Add(Device device) => Items.Add(device);

    public void Remove(Device device) => Items.Remove(device);
}

public class NotificationHandlerTests
{
    private readonly Guid _ali = Guid.NewGuid();
    private readonly Guid _ayse = Guid.NewGuid();
    private readonly InMemoryNotifications _notifications = new();
    private readonly InMemoryDevices _devices = new();
    private readonly RecordingRealtimeNotifier _realtime = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();
    private readonly UserNotifier _notifier;

    public NotificationHandlerTests() => _notifier = new UserNotifier(_notifications, _realtime, _clock);

    private async Task<PagedList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly = false) =>
        await new ListNotificationsQueryHandler(_notifications)
            .Handle(new ListNotificationsQuery(userId, unreadOnly, null, null), CancellationToken.None);

    private async Task<int> UnreadAsync(Guid userId) =>
        (await new GetUnreadNotificationCountQueryHandler(_notifications)
            .Handle(new GetUnreadNotificationCountQuery(userId), CancellationToken.None)).Count;

    private Task MarkReadAsync(Guid userId, Guid notificationId) =>
        new MarkNotificationReadCommandHandler(_notifications, _unitOfWork, _clock)
            .Handle(new MarkNotificationReadCommand(userId, notificationId), CancellationToken.None).AsTask();

    private Task<DeviceDto> RegisterAsync(Guid userId, string token = "token-1") =>
        new RegisterDeviceCommandHandler(_devices, _unitOfWork, _clock)
            .Handle(new RegisterDeviceCommand(userId, token, DevicePlatform.Android), CancellationToken.None).AsTask();

    private static async Task<string> AppCodeOfAsync(Func<Task> action) => (await Assert.ThrowsAsync<AppException>(action)).Code;

    [Fact]
    public async Task A_notification_is_stored_at_once_and_announced_only_when_delivery_is_asked_for()
    {
        var matchId = Guid.NewGuid();

        _notifier.Notify(_ayse, NotificationType.MatchEndRequested, new NotificationData(ActorUserId: _ali, MatchId: matchId));

        var stored = Assert.Single(_notifications.Items);
        Assert.Equal((_ayse, NotificationType.MatchEndRequested, _clock.GetUtcNow()), (stored.UserId, stored.Type, stored.CreatedAt));
        Assert.Empty(_realtime.Sent);

        await _notifier.DeliverAsync(CancellationToken.None);
        await _notifier.DeliverAsync(CancellationToken.None);

        var (userId, eventName, payload) = Assert.Single(_realtime.Sent);
        Assert.Equal((_ayse, RealtimeEvents.NotificationReceived), (userId, eventName));
        var dto = Assert.IsType<NotificationDto>(payload);
        Assert.Equal((stored.Id, matchId, _ali), (dto.Id, dto.Data.MatchId, dto.Data.ActorUserId));
    }

    [Fact]
    public async Task The_pipeline_delivers_after_a_handler_succeeded_and_not_after_one_that_failed()
    {
        var behavior = new NotificationDeliveryBehavior<GetUnreadNotificationCountQuery, UnreadCountDto>(_notifier);
        var query = new GetUnreadNotificationCountQuery(_ali);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await behavior.Handle(
            query,
            (_, _) =>
            {
                _notifier.Notify(_ali, NotificationType.MatchEnded, new NotificationData());
                throw new InvalidOperationException();
            },
            CancellationToken.None));
        Assert.Empty(_realtime.Sent);

        var response = await behavior.Handle(
            query,
            (_, _) =>
            {
                _notifier.Notify(_ayse, NotificationType.ReviewReceived, new NotificationData());
                return ValueTask.FromResult(new UnreadCountDto(7));
            },
            CancellationToken.None);

        Assert.Equal(7, response.Count);
        Assert.Contains(_realtime.Sent, sent => sent.UserId == _ayse);
    }

    [Fact]
    public async Task Notifications_are_listed_newest_first_and_counted_until_read()
    {
        _notifier.Notify(_ali, NotificationType.SwapRequestReceived, new NotificationData());
        _clock.Advance(TimeSpan.FromMinutes(1));
        _notifier.Notify(_ali, NotificationType.SessionProposed, new NotificationData());
        _notifier.Notify(_ayse, NotificationType.ReviewReceived, new NotificationData());

        var all = await ListAsync(_ali);
        Assert.Equal([NotificationType.SessionProposed, NotificationType.SwapRequestReceived], all.Items.Select(item => item.Type));
        Assert.Equal(2, await UnreadAsync(_ali));

        await MarkReadAsync(_ali, all.Items[0].Id);

        Assert.Equal(1, await UnreadAsync(_ali));
        Assert.Equal(NotificationType.SwapRequestReceived, Assert.Single((await ListAsync(_ali, unreadOnly: true)).Items).Type);
        Assert.NotNull((await ListAsync(_ali)).Items[0].ReadAt);

        await new MarkAllNotificationsReadCommandHandler(_notifications, _clock)
            .Handle(new MarkAllNotificationsReadCommand(_ali), CancellationToken.None);

        Assert.Equal(0, await UnreadAsync(_ali));
        Assert.Equal(1, await UnreadAsync(_ayse));
    }

    [Fact]
    public async Task Someone_elses_notification_answers_like_an_unknown_one()
    {
        _notifier.Notify(_ali, NotificationType.MatchEnded, new NotificationData());
        var id = _notifications.Items[0].Id;

        Assert.Equal("not_found", await AppCodeOfAsync(() => MarkReadAsync(_ayse, id)));
        Assert.Equal("not_found", await AppCodeOfAsync(() => MarkReadAsync(_ali, Guid.NewGuid())));
        Assert.Null(_notifications.Items[0].ReadAt);
    }

    [Fact]
    public async Task Notifications_past_their_retention_period_are_removed()
    {
        _notifier.Notify(_ali, NotificationType.MatchEnded, new NotificationData());
        _clock.Advance(Notification.Retention - TimeSpan.FromDays(1));
        _notifier.Notify(_ali, NotificationType.ReviewReceived, new NotificationData());
        var handler = new PurgeOldNotificationsCommandHandler(_notifications, _clock);

        Assert.Equal(0, await handler.Handle(new PurgeOldNotificationsCommand(), CancellationToken.None));

        _clock.Advance(TimeSpan.FromDays(2));

        Assert.Equal(1, await handler.Handle(new PurgeOldNotificationsCommand(), CancellationToken.None));
        Assert.Equal(NotificationType.ReviewReceived, Assert.Single(_notifications.Items).Type);
    }

    [Fact]
    public async Task A_device_is_registered_once_per_token_and_follows_whoever_signed_in_last()
    {
        var first = await RegisterAsync(_ali);
        _clock.Advance(TimeSpan.FromHours(1));
        var again = await RegisterAsync(_ayse);
        await RegisterAsync(_ali, "token-2");

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(_clock.GetUtcNow(), again.LastSeenAt);
        Assert.Equal(2, _devices.Items.Count);
        Assert.Equal(_ayse, _devices.Items[0].UserId);

        var handler = new UnregisterDeviceCommandHandler(_devices, _unitOfWork);
        Assert.Equal(
            "not_found",
            await AppCodeOfAsync(() => handler.Handle(new UnregisterDeviceCommand(_ali, first.Id), CancellationToken.None).AsTask()));
        await handler.Handle(new UnregisterDeviceCommand(_ayse, first.Id), CancellationToken.None);
        Assert.Equal("token-2", Assert.Single(_devices.Items).FcmToken);
    }

    [Fact]
    public void A_device_needs_a_token_of_reasonable_length()
    {
        var validator = new RegisterDeviceCommandValidator();

        Assert.True(validator.Validate(new RegisterDeviceCommand(_ali, "token", DevicePlatform.Ios)).IsValid);
        Assert.False(validator.Validate(new RegisterDeviceCommand(_ali, string.Empty, DevicePlatform.Ios)).IsValid);
        Assert.False(validator.Validate(new RegisterDeviceCommand(_ali, new string('a', 4097), DevicePlatform.Ios)).IsValid);
        Assert.False(validator.Validate(new RegisterDeviceCommand(_ali, "token", (DevicePlatform)9)).IsValid);
    }
}
