using Exparter.Api.Common;
using Exparter.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Exparter.Api.Messaging;

/// <summary>
/// The single real-time connection of a client (docs/realtime.md). It has no methods:
/// clients write through the REST API and only listen here.
/// </summary>
[Authorize]
internal sealed class RealtimeHub : Hub
{
    public const string Path = "/hubs/realtime";
}

/// <summary>Addresses connections by the account id, the same value REST handlers read from the token.</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.Identity?.IsAuthenticated == true ? connection.User.GetUserId().ToString() : null;
}

internal sealed class SignalRRealtimeNotifier(IHubContext<RealtimeHub> hub, ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public async Task SendAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.User(userId.ToString()).SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change is already saved; clients catch up from the REST API.
            logger.LogWarning(exception, "Sending the real-time event {Event} failed", eventName);
        }
    }
}
