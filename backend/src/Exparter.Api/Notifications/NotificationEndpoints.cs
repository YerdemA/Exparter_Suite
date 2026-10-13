using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Notifications;
using Exparter.Domain.Notifications;
using Mediator;

namespace Exparter.Api.Notifications;

internal sealed record RegisterDeviceRequest(string? FcmToken, DevicePlatform Platform);

internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var notifications = routes.MapGroup("/notifications").RequireAuthorization();

        notifications.MapGet("", async (bool? unreadOnly, int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new ListNotificationsQuery(user.GetUserId(), unreadOnly ?? false, page, pageSize),
                cancellationToken)));

        notifications.MapGet("/unread-count", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetUnreadNotificationCountQuery(user.GetUserId()), cancellationToken)));

        notifications.MapPost("/read-all", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new MarkAllNotificationsReadCommand(user.GetUserId()), cancellationToken);
            return Results.NoContent();
        });

        notifications.MapPost("/{notificationId}/read", async (Guid notificationId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new MarkNotificationReadCommand(user.GetUserId(), notificationId), cancellationToken);
            return Results.NoContent();
        });

        var devices = routes.MapGroup("/devices").RequireAuthorization();

        devices.MapPost("", async (RegisterDeviceRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new RegisterDeviceCommand(user.GetUserId(), request.FcmToken ?? string.Empty, request.Platform),
                cancellationToken)));

        devices.MapDelete("/{deviceId}", async (Guid deviceId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new UnregisterDeviceCommand(user.GetUserId(), deviceId), cancellationToken);
            return Results.NoContent();
        });

        return routes;
    }
}
