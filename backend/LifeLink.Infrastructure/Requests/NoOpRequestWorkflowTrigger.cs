using LifeLink.Application.Requests;
using Microsoft.Extensions.Logging;
namespace LifeLink.Infrastructure.Requests;
public sealed class NoOpRequestWorkflowTrigger(ILogger<NoOpRequestWorkflowTrigger> logger) : IRequestWorkflowTrigger
{
    public Task TriggerAsync(Guid requestId, string reason, CancellationToken ct)
    {
        logger.LogInformation("Request {RequestId} queued for workflow trigger: {Reason}", requestId, reason);
        return Task.CompletedTask;
    }
}
