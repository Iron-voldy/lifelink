using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(DeviceTokenRequest input, CancellationToken ct) { var result = await notifications.RegisterDeviceAsync(CurrentUserId(), new(input.Token, input.Platform.ToLowerInvariant()), ct); return result.Succeeded ? NoContent() : Error(result); }
    [HttpDelete("devices")]
    public async Task<IActionResult> RemoveDevice(DeviceTokenDeleteRequest input, CancellationToken ct) { var result = await notifications.RemoveDeviceAsync(CurrentUserId(), input.Token, ct); return result.Succeeded ? NoContent() : Error(result); }
    [HttpPost("broadcast")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<BroadcastResult>> Broadcast(BroadcastRequest input, CancellationToken ct) { var result = await notifications.QueueApprovedBroadcastAsync(new(input.WorkflowExecutionId, input.RecipientUserIds, input.Type, input.Data, input.IdempotencyKey), ct); return result.Succeeded ? Accepted(result.Value) : Error(result); }
    [HttpPost("process-outbox")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<object>> Process([FromQuery] int batchSize = 100, CancellationToken ct = default) => Ok(new { sent = await notifications.ProcessOutboxAsync(batchSize, ct) });
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<NotificationView>>> History(CancellationToken ct) => Ok(await notifications.HistoryAsync(CurrentUserId(), ct));
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private ObjectResult Error<T>(NotificationResult<T> result) => result.ErrorCode switch { "user_not_found" or "device_not_found" => Problem(statusCode: 404, title: result.ErrorCode, detail: result.ErrorMessage), "approval_required" or "token_in_use" => Problem(statusCode: 409, title: result.ErrorCode, detail: result.ErrorMessage), _ => Problem(statusCode: 400, title: result.ErrorCode, detail: result.ErrorMessage) };
}

public sealed record DeviceTokenRequest([Required(ErrorMessage = "Token is required."), MaxLength(500, ErrorMessage = "Token must be at most 500 characters.")] string Token, [Required(ErrorMessage = "Platform is required.")] string Platform);
public sealed record DeviceTokenDeleteRequest([Required(ErrorMessage = "Token is required."), MaxLength(500, ErrorMessage = "Token must be at most 500 characters.")] string Token);
public sealed record BroadcastRequest(Guid WorkflowExecutionId, [MinLength(1, ErrorMessage = "Recipient user ids must include at least one recipient."), MaxLength(1000, ErrorMessage = "Recipient user ids must not exceed 1000 recipients.")] IReadOnlyList<Guid> RecipientUserIds, [Required(ErrorMessage = "Type is required."), MaxLength(100, ErrorMessage = "Type must be at most 100 characters.")] string Type, IReadOnlyDictionary<string, string> Data, [Required(ErrorMessage = "Idempotency key is required."), MaxLength(64, ErrorMessage = "Idempotency key must be at most 64 characters.")] string IdempotencyKey);
