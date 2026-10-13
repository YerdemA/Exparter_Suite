using Exparter.Application.Matching;
using Mediator;

namespace Exparter.Api.Matching;

/// <summary>
/// Periodically records swap requests and match end requests whose deadline has passed.
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
                var processed = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new ProcessMatchingDeadlinesCommand(), stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Recorded {Count} passed matching deadlines", processed);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One failed run must not stop the worker; the next tick tries again.
                logger.LogError(exception, "Processing matching deadlines failed");
            }
        }
    }
}
