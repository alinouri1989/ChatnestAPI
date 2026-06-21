using ChatNest.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace ChatNest.API.Controllers
{
    [Route("api/[controller]")]
    public sealed class CallController : BaseController
    {
        private readonly ICallService _callService;
        private readonly IUserService _userService;

        public CallController(ICallService callService, IUserService userService)
        {
            _callService = callService;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 100);

            var (calls, recipientIds, totalCalls) = await _callService.GetCallLogsAsync(UserId, skip, take);
            var recipientProfiles = await _userService.GetUserProfilesAsync(recipientIds ?? new List<string>());

            return Ok(new
            {
                calls,
                recipientProfiles,
                totalCalls,
                skip,
                take,
                hasMore = skip + take < totalCalls
            });
        }

        [HttpGet("Total")]
        public async Task<ActionResult<int>> Total()
        {
            return Ok(await _callService.GetUserCallsCountAsync(UserId));
        }
    }
}
