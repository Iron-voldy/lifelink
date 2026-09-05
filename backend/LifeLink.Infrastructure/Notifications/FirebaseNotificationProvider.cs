using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Apis.Auth.OAuth2;
using LifeLink.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLink.Infrastructure.Notifications;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";
    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string ServiceAccountJsonBase64 { get; set; } = string.Empty;
}

public sealed class FirebaseNotificationProvider(HttpClient client, IOptions<FirebaseOptions> options, ILogger<FirebaseNotificationProvider> logger) : INotificationProvider
{
    private readonly FirebaseOptions _options = options.Value;

    public async Task<ProviderResult> SendAsync(NotificationDelivery delivery, CancellationToken ct)
    {
        try
        {
            var credentialJson = string.IsNullOrWhiteSpace(_options.ServiceAccountJsonBase64) ? _options.ServiceAccountJson : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(_options.ServiceAccountJsonBase64));
            var credential = GoogleCredential.FromJson(credentialJson)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
            var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: ct);
            var payload = new Dictionary<string, string>(delivery.Data) { ["type"] = delivery.Type };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/messages:send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new { message = new { token = delivery.DeviceToken, notification = new { title = "LifeLink", body = delivery.Data.TryGetValue("message", out var body) ? body : delivery.Type.Replace('-', ' ') }, data = payload } });
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return new ProviderResult(true, null);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("FCM rejected {NotificationType} with status {StatusCode}", delivery.Type, (int)response.StatusCode);
            return new ProviderResult(false, $"FCM returned {(int)response.StatusCode}: {SafeError(responseBody)}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ProviderResult(false, "FCM request timed out.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FCM delivery failed for {NotificationType}", delivery.Type);
            return new ProviderResult(false, "FCM delivery failed safely.");
        }
    }

    private static string SafeError(string value) => value.Length > 300 ? value[..300] : value;
}
