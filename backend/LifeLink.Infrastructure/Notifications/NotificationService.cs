using System.Text.Json;
using LifeLink.Application.Notifications;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Infrastructure.Notifications;

public sealed class NotificationService(LifeLinkDbContext db, INotificationProvider provider, TimeProvider timeProvider) : INotificationService
{
    public async Task<NotificationResult<bool>> RegisterDeviceAsync(Guid userId, DeviceTokenInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Token) || input.Token.Length > 500 || input.Platform is not ("android" or "ios" or "web")) return NotificationResult<bool>.Failure("invalid_device_token", "Token or platform is invalid.");
        if (!await db.Users.AnyAsync(x => x.Id == userId && x.IsActive, ct)) return NotificationResult<bool>.Failure("user_not_found", "User was not found.");
        var token = await db.DeviceTokens.SingleOrDefaultAsync(x => x.Token == input.Token, ct);
        if (token is null) db.DeviceTokens.Add(new DeviceToken(userId, input.Token, input.Platform));
        else if (token.UserId != userId) return NotificationResult<bool>.Failure("token_in_use", "Device token is linked to another account.");
        else token.Touch(input.Platform, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct); return NotificationResult<bool>.Success(true);
    }

    public async Task<NotificationResult<bool>> RemoveDeviceAsync(Guid userId, string rawToken, CancellationToken ct)
    {
        var token = await db.DeviceTokens.SingleOrDefaultAsync(x => x.UserId == userId && x.Token == rawToken, ct); if (token is null) return NotificationResult<bool>.Failure("device_not_found", "Device token was not found.");
        token.Deactivate(); await db.SaveChangesAsync(ct); return NotificationResult<bool>.Success(true);
    }

    public async Task<NotificationResult<BroadcastResult>> QueueApprovedBroadcastAsync(BroadcastInput input, CancellationToken ct)
    {
        if (input.RecipientUserIds.Count is < 1 or > 1000 || string.IsNullOrWhiteSpace(input.Type) || input.Type.Length > 100 || string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 80) return NotificationResult<BroadcastResult>.Failure("invalid_broadcast", "Recipients, type, or idempotency key is invalid.");
        var approved = await db.AgentApprovals.AnyAsync(x => x.WorkflowExecutionId == input.WorkflowExecutionId && x.Decision == ApprovalDecision.Approved, ct);
        if (!approved) return NotificationResult<BroadcastResult>.Failure("approval_required", "An approved workflow decision is required before broadcasting.");
        var validUsers = await db.Users.Where(x => input.RecipientUserIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(ct); var ids = new List<Guid>(); var duplicates = 0;
        foreach (var userId in validUsers.Distinct())
        {
            var key = $"{input.IdempotencyKey}:{userId:N}"; var existing = await db.Notifications.Where(x => x.IdempotencyKey == key).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (existing is not null) { duplicates++; ids.Add(existing.Value); continue; }
            var notification = new Notification(userId, input.WorkflowExecutionId, input.Type.Trim(), "Push", JsonSerializer.Serialize(input.Data), key); db.Notifications.Add(notification); ids.Add(notification.Id);
        }
        await db.SaveChangesAsync(ct); return NotificationResult<BroadcastResult>.Success(new(ids.Count - duplicates, duplicates, ids));
    }

    public async Task<NotificationResult<Guid>> QueueUserNotificationAsync(Guid userId, string type, IReadOnlyDictionary<string, string> data, string idempotencyKey, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId && x.IsActive, ct)) return NotificationResult<Guid>.Failure("user_not_found", "User was not found.");
        var existing = await db.Notifications.Where(x => x.IdempotencyKey == idempotencyKey).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct); if (existing is not null) return NotificationResult<Guid>.Success(existing.Value);
        var notification = new Notification(userId, null, type, "Push", JsonSerializer.Serialize(data), idempotencyKey); db.Notifications.Add(notification); await db.SaveChangesAsync(ct); return NotificationResult<Guid>.Success(notification.Id);
    }

    public async Task<int> ProcessOutboxAsync(int batchSize, CancellationToken ct)
    {
        batchSize = Math.Clamp(batchSize, 1, 500); var items = await db.Notifications.Where(x => (x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Failed) && x.AttemptCount < 3).OrderBy(x => x.CreatedAtUtc).Take(batchSize).ToListAsync(ct); var sent = 0;
        foreach (var item in items)
        {
            var devices = await db.DeviceTokens.Where(x => x.UserId == item.RecipientUserId && x.IsActive).Select(x => x.Token).ToListAsync(ct);
            if (devices.Count == 0) { item.MarkFailed("No active device token."); continue; }
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(item.PayloadJson) ?? []; string? error = null;
            foreach (var device in devices) { var result = await provider.SendAsync(new(device, item.Type, data), ct); if (!result.Succeeded) { error = result.Error ?? "Provider rejected notification."; break; } }
            if (error is null) { item.MarkSent(timeProvider.GetUtcNow()); sent++; } else item.MarkFailed(error);
        }
        await db.SaveChangesAsync(ct); return sent;
    }

    public async Task<IReadOnlyList<NotificationView>> HistoryAsync(Guid userId, CancellationToken ct) => await db.Notifications.AsNoTracking().Where(x => x.RecipientUserId == userId).OrderByDescending(x => x.CreatedAtUtc).Take(200).Select(x => new NotificationView(x.Id, x.RecipientUserId, x.WorkflowExecutionId, x.Type, x.Channel, x.Status, x.AttemptCount, x.SentAtUtc, x.LastError, x.CreatedAtUtc)).ToListAsync(ct);
}
