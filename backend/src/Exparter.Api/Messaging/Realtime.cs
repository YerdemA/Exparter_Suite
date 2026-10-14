using Exparter.Api.Common;
using System.Collections.Concurrent;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Exparter.Api.Messaging;

/// <summary>
/// The single real-time connection of a client (docs/realtime.md). It has no methods:
/// clients write through the REST API and only listen here.
/// </summary>
[Authorize]
internal sealed class RealtimeHub(ConnectionRegistry connections, IUserDirectory users) : Hub
{
    public const string Path = "/hubs/realtime";

    public override async Task OnConnectedAsync()
    {
        // An access token outlives a suspension or a deletion request by a few minutes;
        // the connection must not.
        var userId = Context.User!.GetUserId();
        if (await users.FindAsync(userId, Context.ConnectionAborted) is not { IsActive: true })
        {
            Context.Abort();
            return;
        }

        connections.Add(userId, Context);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.User!.GetUserId(), Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// The open connections of each user: push messages go only to users with no app open,
/// and an account that stops being active is disconnected. It lives in this process:
/// running several instances needs a shared one.
/// </summary>
internal sealed class ConnectionRegistry : IUserPresence, IRealtimeConnections
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, HubCallerContext>> _connections = new();

    public bool IsConnected(Guid userId) => _connections.TryGetValue(userId, out var open) && !open.IsEmpty;

    public void Add(Guid userId, HubCallerContext context) =>
        _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, HubCallerContext>())[context.ConnectionId] = context;

    public void Remove(Guid userId, string connectionId)
    {
        if (_connections.TryGetValue(userId, out var open))
        {
            open.TryRemove(connectionId, out _);
        }
    }

    public void CloseAll(Guid userId)
    {
        if (_connections.TryGetValue(userId, out var open))
        {
            foreach (var context in open.Values)
            {
                context.Abort();
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
