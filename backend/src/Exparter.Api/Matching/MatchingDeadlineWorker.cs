using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Moderation;
using Exparter.Application.Notifications;
using Exparter.Application.Sessions;
using Mediator;

namespace Exparter.Api.Matching;

/// <summary>
/// Periodically records the deadlines that have passed (swap requests, match end
/// requests, session proposals and session completion marks), sends the reminders that
/// are due, removes notifications and closed reports past their retention period and erases the accounts
/// whose deletion can no longer be taken back, and closes the video calls that have ended.
/// Reads never depend on it having run (they compare deadlines with the clock); it only
/// makes the stored state catch up.
/// </summary>
internal sealed class MatchingDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<MatchingDeadlineWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var processed = await sender.Send(new ProcessMatchingDeadlinesCommand(), stoppingToken)
                    + await sender.Send(new ProcessSessionDeadlinesCommand(), stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Recorded {Count} passed deadlines and reminders", processed);
                }

                await sender.Send(new PurgeOldNotificationsCommand(), stoppingToken);
                await sender.Send(new PurgeOldReportsCommand(), stoppingToken);
                await sender.Send(new EraseDeletedAccountsCommand(), stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One failed run must not stop the worker; the next tick tries again.
                logger.LogError(exception, "Processing deadlines failed");
            }

            // Apart from the rest, so that the media server being down does not hold up the deadlines.
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var closed = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CloseEndedCallsCommand(), stoppingToken);
                if (closed > 0)
                {
                    logger.LogInformation("Closed {Count} ended calls", closed);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Closing ended calls failed");
            }
        }
    }
}
