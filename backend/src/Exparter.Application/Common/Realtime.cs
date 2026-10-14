namespace Exparter.Application.Common;

/// <summary>The server-to-client events of docs/realtime.md.</summary>
public static class RealtimeEvents
{
    public const string MessageReceived = "MessageReceived";
    public const string ConversationRead = "ConversationRead";
    public const string ConversationUpdated = "ConversationUpdated";
    public const string NotificationReceived = "NotificationReceived";
}

/// <summary>
/// Pushes an event to every open connection of a user. An event is only a hint that
/// something changed: delivery is not guaranteed and a failure to deliver never fails
/// the request that caused it.
/// </summary>
public interface IRealtimeNotifier
{
    Task SendAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken);
}

/// <summary>Ends the open real-time connections of a user whose account stopped being active.</summary>
public interface IRealtimeConnections
{
    void CloseAll(Guid userId);
}
