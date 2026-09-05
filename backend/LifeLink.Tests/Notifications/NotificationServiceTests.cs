using LifeLink.Application.Notifications;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Notifications;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task BroadcastWithoutApprovalIsRejected()
    {
        var (service, _, user, _, _) = Create();
        var result = await service.QueueApprovedBroadcastAsync(new(Guid.NewGuid(), [user.Id], "urgent-request", new Dictionary<string, string>(), "broadcast-1"), default);
        Assert.Equal("approval_required", result.ErrorCode);
    }

    [Fact]
    public async Task ApprovedBroadcastIsIdempotentAndDeliveredFromOutbox()
    {
        var (service, db, user, workflow, provider) = Create();
        db.DeviceTokens.Add(new DeviceToken(user.Id, "device-token-123456", "android")); db.AgentApprovals.Add(new AgentApproval(workflow.Id, 1, user.Id, ApprovalDecision.Approved, "Approved", DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        var input = new BroadcastInput(workflow.Id, [user.Id], "urgent-request", new Dictionary<string, string> { ["requestId"] = "123" }, "broadcast-2");
        var first = await service.QueueApprovedBroadcastAsync(input, default); var replay = await service.QueueApprovedBroadcastAsync(input, default); var sent = await service.ProcessOutboxAsync(10, default);
        Assert.Equal(1, first.Value!.Queued); Assert.Equal(1, replay.Value!.Duplicates); Assert.Equal(1, sent); Assert.Single(provider.Deliveries); Assert.Equal(NotificationStatus.Sent, (await db.Notifications.SingleAsync()).Status);
    }

    private static (NotificationService Service, LifeLinkDbContext Db, User User, AgentWorkflowExecution Workflow, CapturingProvider Provider) Create()
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; var db = new LifeLinkDbContext(options); var user = new User("donor@example.com", "hash", UserRole.Donor); var workflow = new AgentWorkflowExecution(Guid.NewGuid(), 1, "test", Guid.NewGuid().ToString("N")); db.Users.Add(user); db.AgentWorkflowExecutions.Add(workflow); db.SaveChanges(); var provider = new CapturingProvider(); return (new NotificationService(db, provider, TimeProvider.System), db, user, workflow, provider);
    }
    private sealed class CapturingProvider : INotificationProvider
    {
        public List<NotificationDelivery> Deliveries { get; } = [];
        public Task<ProviderResult> SendAsync(NotificationDelivery delivery, CancellationToken ct) { Deliveries.Add(delivery); return Task.FromResult(new ProviderResult(true, null)); }
    }
}
