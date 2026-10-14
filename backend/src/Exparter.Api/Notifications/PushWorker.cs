using System.Threading.Channels;
using Exparter.Application.Notifications;
using Mediator;

namespace Exparter.Api.Notifications;

/// <summary>
/// Push messages waiting to be sent. Pushes are best effort: when the queue is full the
/// oldest give way, and whatever is queued when the process stops is lost.
/// </summary>
internal sealed class PushQueue : IPushQueue
{
    private readonly Channel<SendPushCommand> _channel =
        Channel.CreateBounded<SendPushCommand>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<SendPushCommand> Reader => _channel.Reader;

    public void Enqueue(Guid userId, PushMessage message) => _channel.Writer.TryWrite(new SendPushCommand(userId, message));
}

/// <summary>Sends the queued push messages one by one, outside of the requests that caused them.</summary>
internal sealed class PushWorker(PushQueue queue, IServiceScopeFactory scopeFactory, ILogger<PushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The notification is stored and listed in the app either way.
                logger.LogWarning(exception, "Sending a push message failed");
            }
        }
    }
}
