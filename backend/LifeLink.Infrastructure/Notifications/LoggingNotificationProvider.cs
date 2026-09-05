using LifeLink.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace LifeLink.Infrastructure.Notifications;

public sealed class LoggingNotificationProvider(ILogger<LoggingNotificationProvider> logger) : INotificationProvider
{
    public Task<ProviderResult> SendAsync(NotificationDelivery delivery, CancellationToken ct)
    {
        logger.LogInformation("Simulated push notification {Type} to device token ending {Suffix}", delivery.Type, delivery.DeviceToken.Length > 6 ? delivery.DeviceToken[^6..] : "hidden");
        return Task.FromResult(new ProviderResult(true, null));
    }
}
