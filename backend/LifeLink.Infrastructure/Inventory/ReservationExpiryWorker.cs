using LifeLink.Application.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLink.Infrastructure.Inventory;

public sealed class ReservationExpiryOptions
{
    public bool Enabled { get; set; } = true;
    public int PollSeconds { get; set; } = 60;
}

/// <summary>Returns units from lapsed holds to stock so availability never stays understated until someone reserves again.</summary>
public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes, IOptions<ReservationExpiryOptions> options, ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    private readonly ReservationExpiryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(_options.PollSeconds, 10, 3600)));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<IInventoryService>().ExpireReservationsAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Expired {ReservationCount} lapsed stock reservations", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Reservation expiry failed; the next poll will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
