using Exparter.Api.Common;
using System.Collections.Concurrent;
using Exparter.Application.Common;
using Exparter.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Exparter.Api.Messaging;

/// <summary>
/// The single real-time connection of a client (docs/realtime.md). It has no methods:
/// clients write through the REST API and only listen here.
/// </summary>
[Authorize]
internal sealed class RealtimeHub(ConnectionRegistry connections) : Hub
{
    public const string Path = "/hubs/realtime";

    public override Task OnConnectedAsync()
    {
        connections.Add(Context.User!.GetUserId());
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.User!.GetUserId());
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Counts the open connections of each user, so that push messages go only to users with
/// no app open. It lives in this process: running several instances needs a shared one.
/// </summary>
internal sealed class ConnectionRegistry : IUserPresence
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();

    public bool IsConnected(Guid userId) => _counts.ContainsKey(userId);

    public void Add(Guid userId) => _counts.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public void Remove(Guid userId)
    {
        while (_counts.TryGetValue(userId, out var count))
        {
            var removed = count <= 1
                ? _counts.TryRemove(new KeyValuePair<Guid, int>(userId, count))
                : _counts.TryUpdate(userId, count - 1, count);
            if (removed)
            {
                return;
            }
        }
    }
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
