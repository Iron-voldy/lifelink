using LifeLink.Domain.Enums;

namespace LifeLink.Application.Notifications;

public sealed record DeviceTokenInput(string Token, string Platform);
public sealed record NotificationView(Guid Id, Guid RecipientUserId, Guid? WorkflowExecutionId, string Type, string Channel, NotificationStatus Status, int AttemptCount, DateTimeOffset? SentAtUtc, string? LastError, DateTimeOffset CreatedAtUtc);
public sealed record BroadcastInput(Guid WorkflowExecutionId, IReadOnlyList<Guid> RecipientUserIds, string Type, IReadOnlyDictionary<string, string> Data, string IdempotencyKey);
public sealed record BroadcastResult(int Queued, int Duplicates, IReadOnlyList<Guid> NotificationIds);
public sealed record NotificationDelivery(string DeviceToken, string Type, IReadOnlyDictionary<string, string> Data);
public sealed record ProviderResult(bool Succeeded, string? Error);
public sealed record NotificationResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;
    public static NotificationResult<T> Success(T value) => new(value, null, null);
    public static NotificationResult<T> Failure(string code, string message) => new(default, code, message);
}
public interface INotificationProvider { Task<ProviderResult> SendAsync(NotificationDelivery delivery, CancellationToken ct); }
public interface INotificationService
{
    Task<NotificationResult<bool>> RegisterDeviceAsync(Guid userId, DeviceTokenInput input, CancellationToken ct);
    Task<NotificationResult<bool>> RemoveDeviceAsync(Guid userId, string token, CancellationToken ct);
    Task<NotificationResult<BroadcastResult>> QueueApprovedBroadcastAsync(BroadcastInput input, CancellationToken ct);
    Task<NotificationResult<Guid>> QueueUserNotificationAsync(Guid userId, string type, IReadOnlyDictionary<string, string> data, string idempotencyKey, CancellationToken ct);
    Task<int> ProcessOutboxAsync(int batchSize, CancellationToken ct);
    Task<IReadOnlyList<NotificationView>> HistoryAsync(Guid userId, CancellationToken ct);
}
