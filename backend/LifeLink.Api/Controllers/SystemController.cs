using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("info")]
    public ActionResult<SystemInfoResponse> GetInfo() => Ok(new SystemInfoResponse("LifeLink API", "v1", DateTimeOffset.UtcNow));
}

public sealed record SystemInfoResponse(string Name, string Version, DateTimeOffset ServerTimeUtc);
