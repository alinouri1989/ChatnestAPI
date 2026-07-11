using ChatNest.API.AppVersioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatNest.DataAccess.Abstract;

namespace ChatNest.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/app-version")]
public sealed class AppVersionController(IAppVersionPolicyRepository policies) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AppVersionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppVersionResponse>> Get(
        [FromQuery] string? platform,
        [FromQuery] int? build,
        [FromQuery] string? version,
        [FromQuery] string channel = "production")
    {
        if (string.IsNullOrWhiteSpace(platform))
            ModelState.AddModelError(nameof(platform), "Platform is required.");
        if (build is null or < 0)
            ModelState.AddModelError(nameof(build), "Build must be a non-negative integer.");
        if (string.IsNullOrWhiteSpace(version))
            ModelState.AddModelError(nameof(version), "Version is required.");
        if (string.IsNullOrWhiteSpace(channel))
            ModelState.AddModelError(nameof(channel), "Channel is required.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var normalizedPlatform = platform!.Trim().ToLowerInvariant();
        var normalizedChannel = channel.Trim().ToLowerInvariant();
        var policy = await policies.FindAsync(normalizedPlatform, normalizedChannel, HttpContext.RequestAborted);

        if (policy is null)
            return NotFound(new { message = "No version policy exists for this platform and channel." });

        return Ok(AppVersionPolicyEvaluator.Evaluate(policy, normalizedPlatform, build!.Value, version!.Trim()));
    }
}
