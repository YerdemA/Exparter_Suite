using Exparter.Application.Common;
using Exparter.Domain.Notifications;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Notifications;

public interface INotificationRepository
{
    /// <summary>Newest first.</summary>
    Task<PagedList<Notification>> ListAsync(Guid userId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <returns>How many notifications were removed.</returns>
    Task<int> DeleteCreatedBeforeAsync(DateTimeOffset moment, CancellationToken cancellationToken);

    void Add(Notification notification);
}

public interface IDeviceRepository
{
    Task<Device?> FindByTokenAsync(string fcmToken, CancellationToken cancellationToken);

    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Device device);

    void Remove(Device device);
}

/// <summary>
/// How other modules tell a user that something happened. The notification is stored
/// with the unit of work of the request that caused it, and reaches the user's open
/// connections once that request has succeeded.
/// </summary>
public interface IUserNotifier
{
    void Notify(Guid userId, NotificationType type, NotificationData data);
}

public sealed record NotificationDto(Guid Id, NotificationType Type, NotificationData Data, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt)
{
    public static NotificationDto From(Notification notification) =>
        new(notification.Id, notification.Type, notification.Data, notification.CreatedAt, notification.ReadAt);
}

internal sealed class UserNotifier(INotificationRepository notifications, IRealtimeNotifier realtime, TimeProvider timeProvider)
    : IUserNotifier
{
    private readonly List<Notification> _undelivered = [];

    public void Notify(Guid userId, NotificationType type, NotificationData data)
    {
        var notification = Notification.Create(userId, type, data, timeProvider.GetUtcNow());
        notifications.Add(notification);
        _undelivered.Add(notification);
    }

    /// <summary>Pushes what was stored since the last call to the users' open connections.</summary>
    public async Task DeliverAsync(CancellationToken cancellationToken)
    {
        var batch = _undelivered.ToList();
        _undelivered.Clear();
        foreach (var notification in batch)
        {
            await realtime.SendAsync(
                notification.UserId,
                RealtimeEvents.NotificationReceived,
                NotificationDto.From(notification),
                cancellationToken);
        }
    }
}

/// <summary>
/// Delivers the notifications a request produced, after its handler has finished: a
/// handler that fails has stored nothing, so nothing is announced either.
/// </summary>
public sealed class NotificationDeliveryBehavior<TMessage, TResponse>(IUserNotifier notifier)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);
        if (notifier is UserNotifier userNotifier)
        {
            await userNotifier.DeliverAsync(cancellationToken);
        }

        return response;
    }
}

public sealed record ListNotificationsQuery(Guid UserId, bool UnreadOnly, int? Page, int? PageSize) : IQuery<PagedList<NotificationDto>>;

public sealed class ListNotificationsQueryHandler(INotificationRepository notifications)
    : IQueryHandler<ListNotificationsQuery, PagedList<NotificationDto>>
{
    public async ValueTask<PagedList<NotificationDto>> Handle(ListNotificationsQuery query, CancellationToken cancellationToken) =>
        (await notifications.ListAsync(query.UserId, query.UnreadOnly, PageRequest.From(query.Page, query.PageSize), cancellationToken))
        .Map(NotificationDto.From);
}

public sealed record UnreadCountDto(int Count);

public sealed record GetUnreadNotificationCountQuery(Guid UserId) : IQuery<UnreadCountDto>;

public sealed class GetUnreadNotificationCountQueryHandler(INotificationRepository notifications)
    : IQueryHandler<GetUnreadNotificationCountQuery, UnreadCountDto>
{
    public async ValueTask<UnreadCountDto> Handle(GetUnreadNotificationCountQuery query, CancellationToken cancellationToken) =>
        new(await notifications.CountUnreadAsync(query.UserId, cancellationToken));
}

public sealed record MarkNotificationReadCommand(Guid UserId, Guid NotificationId) : ICommand;

public sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<MarkNotificationReadCommand>
{
    public async ValueTask<Unit> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        // Someone else's notification answers like an unknown id, so that ids cannot be probed.
        if (await notifications.GetByIdAsync(command.NotificationId, cancellationToken) is not { } notification
            || notification.UserId != command.UserId)
        {
            throw AppException.NotFound("The notification was not found.");
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record MarkAllNotificationsReadCommand(Guid UserId) : ICommand;

public sealed class MarkAllNotificationsReadCommandHandler(INotificationRepository notifications, TimeProvider timeProvider)
    : ICommandHandler<MarkAllNotificationsReadCommand>
{
    public async ValueTask<Unit> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Removes notifications older than the retention period.</summary>
public sealed record PurgeOldNotificationsCommand : ICommand<int>;

public sealed class PurgeOldNotificationsCommandHandler(INotificationRepository notifications, TimeProvider timeProvider)
    : ICommandHandler<PurgeOldNotificationsCommand, int>
{
    /// <returns>How many notifications were removed.</returns>
    public async ValueTask<int> Handle(PurgeOldNotificationsCommand command, CancellationToken cancellationToken) =>
        await notifications.DeleteCreatedBeforeAsync(timeProvider.GetUtcNow() - Notification.Retention, cancellationToken);
}

public sealed record DeviceDto(Guid Id, DevicePlatform Platform, DateTimeOffset LastSeenAt)
{
    public static DeviceDto From(Device device) => new(device.Id, device.Platform, device.LastSeenAt);
}

public sealed record RegisterDeviceCommand(Guid UserId, string FcmToken, DevicePlatform Platform) : ICommand<DeviceDto>;

public sealed class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.FcmToken).NotEmpty().MaximumLength(Device.TokenMaxLength);
        RuleFor(x => x.Platform).IsInEnum();
    }
}

public sealed class RegisterDeviceCommandHandler(IDeviceRepository devices, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<RegisterDeviceCommand, DeviceDto>
{
    public async ValueTask<DeviceDto> Handle(RegisterDeviceCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var device = await devices.FindByTokenAsync(command.FcmToken, cancellationToken);
        if (device is null)
        {
            device = Device.Register(command.UserId, command.FcmToken, command.Platform, now);
            devices.Add(device);
        }
        else
        {
            device.Refresh(command.UserId, command.Platform, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DeviceDto.From(device);
    }
}

public sealed record UnregisterDeviceCommand(Guid UserId, Guid DeviceId) : ICommand;

public sealed class UnregisterDeviceCommandHandler(IDeviceRepository devices, IUnitOfWork unitOfWork)
    : ICommandHandler<UnregisterDeviceCommand>
{
    public async ValueTask<Unit> Handle(UnregisterDeviceCommand command, CancellationToken cancellationToken)
    {
        if (await devices.GetByIdAsync(command.DeviceId, cancellationToken) is not { } device || device.UserId != command.UserId)
        {
            throw AppException.NotFound("The device was not found.");
        }

        devices.Remove(device);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
