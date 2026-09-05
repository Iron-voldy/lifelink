using LifeLink.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLink.Infrastructure.Notifications;

public sealed class NotificationOutboxOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 100;
    public int PollSeconds { get; set; } = 10;
}

public sealed class NotificationOutboxWorker(IServiceScopeFactory scopes, IOptions<NotificationOutboxOptions> options, ILogger<NotificationOutboxWorker> logger) : BackgroundService
{
    private readonly NotificationOutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(_options.PollSeconds, 2, 300)));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sent = await scope.ServiceProvider.GetRequiredService<INotificationService>().ProcessOutboxAsync(_options.BatchSize, stoppingToken);
                if (sent > 0) logger.LogInformation("Delivered {NotificationCount} queued notifications", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification outbox processing failed; the next bounded poll will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
