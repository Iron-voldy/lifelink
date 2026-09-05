using LifeLink.Application.Notifications;
using LifeLink.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LifeLink.Tests.Notifications;

public sealed class FirebaseNotificationProviderTests
{
    [Fact]
    public async Task InvalidCredentialsFailSafelyWithoutLeakingConfiguration()
    {
        var provider = new FirebaseNotificationProvider(new HttpClient(), Options.Create(new FirebaseOptions { Enabled = true, ProjectId = "test-project", ServiceAccountJson = "not-json" }), NullLogger<FirebaseNotificationProvider>.Instance);
        var result = await provider.SendAsync(new NotificationDelivery("device-secret", "urgent-request", new Dictionary<string, string> { ["requestId"] = "123" }), default);
        Assert.False(result.Succeeded);
        Assert.Equal("FCM delivery failed safely.", result.Error);
        Assert.DoesNotContain("device-secret", result.Error);
    }
}
