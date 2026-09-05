namespace LifeLink.Application.Requests;

public interface IRequestWorkflowTrigger
{
    Task TriggerAsync(Guid requestId, string reason, CancellationToken ct);
}
