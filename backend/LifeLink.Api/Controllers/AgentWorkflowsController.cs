using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/agent-workflows")]
public sealed class AgentWorkflowsController(IWorkflowService workflows) : ControllerBase
{
    [HttpPost("start")]
    public async Task<ActionResult<WorkflowView>> Start(StartWorkflowRequest input, CancellationToken ct) { var result = await workflows.StartAsync(input.RequestId, input.Reason ?? "manual-start", ct); return result.Succeeded ? Accepted(result.Value) : Error(result); }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowView>> Get(Guid id, CancellationToken ct) { var result = await workflows.GetAsync(id, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowView>>> List([FromQuery] LifeLink.Domain.Enums.WorkflowStatus? status, [FromQuery] int limit = 100, CancellationToken ct = default) => Ok(await workflows.ListAsync(status, limit, ct));
    [HttpGet("{id:guid}/execution-summary")]
    public async Task<ActionResult<WorkflowView>> Summary(Guid id, CancellationToken ct) { var result = await workflows.GetAsync(id, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<WorkflowView>> Approve(Guid id, WorkflowDecisionRequest input, CancellationToken ct) { var result = await workflows.ApproveAsync(id, CurrentUserId(), input.Comments, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<WorkflowView>> Reject(Guid id, WorkflowDecisionRequest input, CancellationToken ct) { var result = await workflows.RejectAsync(id, CurrentUserId(), input.Comments, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpPost("{id:guid}/revise")]
    public async Task<ActionResult<WorkflowView>> Revise(Guid id, WorkflowRevisionRequest input, CancellationToken ct) { var result = await workflows.ReviseAsync(id, CurrentUserId(), input.Comments, ct); return result.Succeeded ? Accepted(result.Value) : Error(result); }
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private ObjectResult Error<T>(WorkflowResult<T> result) => result.ErrorCode switch { "workflow_not_found" or "request_not_found" => Problem(statusCode: 404, title: result.ErrorCode, detail: result.ErrorMessage), "workflow_not_pending" or "request_not_pending" or "request_in_progress" or "workflow_active" or "workflow_conflict" or "request_closed" or "stock_unavailable" or "concurrency_conflict" => Problem(statusCode: 409, title: result.ErrorCode, detail: result.ErrorMessage), "invalid_workflow_output" => Problem(statusCode: 422, title: result.ErrorCode, detail: result.ErrorMessage), "approval_actions_failed" => Problem(statusCode: 502, title: result.ErrorCode, detail: result.ErrorMessage), _ => Problem(statusCode: 400, title: result.ErrorCode, detail: result.ErrorMessage) };
}

public sealed record StartWorkflowRequest(Guid RequestId, [MaxLength(500, ErrorMessage = "Reason must be at most 500 characters.")] string? Reason);
public sealed record WorkflowDecisionRequest([MaxLength(2000, ErrorMessage = "Comments must be at most 2000 characters.")] string? Comments);
public sealed record WorkflowRevisionRequest([Required(ErrorMessage = "Comments is required."), MaxLength(2000, ErrorMessage = "Comments must be at most 2000 characters.")] string Comments);
