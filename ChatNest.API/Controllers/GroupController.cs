using ChatNest.API.Hubs;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;
using Firebase.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChatNest.API.Controllers
{
    /// <summary>
    /// کنترلر API برای انجام عملیات مرتبط با گروه‌ها.
    /// کاربران می‌توانند گروه ایجاد کنند، از گروه خارج شوند و اطلاعات گروه را به‌روزرسانی کنند.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    public sealed class GroupController : BaseController
    {
        private readonly IHubContext<NotificationHub> _notificationHubContext;
        private readonly IGroupService _groupService;
        private readonly IChatService _chatService;

        /// <summary>
        /// سازنده کلاس GroupController.
        /// سرویس‌های مورد نیاز را دریافت کرده و کنترلر را راه‌اندازی می‌کند.
        /// </summary>
        /// <param name="notificationHubContext">شیء <see cref="IHubContext{NotificationHub}"/> برای هاب اعلان‌ها.</param>
        /// <param name="groupService">شیء <see cref="IGroupService"/> برای عملیات گروه.</param>
        /// <param name="chatService">شیء <see cref="IChatService"/> برای عملیات گفتگو.</param>
        public GroupController(IHubContext<NotificationHub> notificationHubContext, IGroupService groupService, IChatService chatService)
        {
            _notificationHubContext = notificationHubContext;
            _groupService = groupService;
            _chatService = chatService;
        }

        /// <summary>
        /// یک گروه جدید ایجاد می‌کند.
        /// </summary>
        /// <param name="dto">شیء انتقال داده <see cref="CreateGroup"/> که حاوی اطلاعات مورد نیاز برای ایجاد گروه است.</param>
        /// <returns>اطلاعات گروه جدید ایجاد شده به همراه وضعیت موفقیت برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت نامعتبر بودن مدل، پیام خطا برمی‌گرداند.</exception>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد، پیام خطا برمی‌گرداند.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره، پیام خطا برمی‌گرداند.</exception>
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateGroup dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var group = await _groupService.CreateGroupAsync(UserId, dto);

                await _chatService.CreateChatAsync(UserId, "Group", group.Keys.First());

                foreach (var participant in group.Values.First().Participants.Keys.ToList())
                {
                    await _notificationHubContext.Clients.User(participant).SendAsync("ReceiveNewGroupProfiles", group);
                }

                return Ok(new { message = "گروه ایجاد شد." });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FirebaseException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// اطلاعات یک گروه موجود را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="groupId">شناسه گروهی که قرار است ویرایش شود.</param>
        /// <param name="dto">شیء انتقال داده <see cref="CreateGroup"/> که حاوی اطلاعات مورد نیاز برای ویرایش گروه است.</param>
        /// <returns>پیام موفقیت‌آمیز بودن به‌روزرسانی اطلاعات گروه برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت نامعتبر بودن مدل، پیام خطا برمی‌گرداند.</exception>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد، پیام خطا برمی‌گرداند.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره، پیام خطا برمی‌گرداند.</exception>
        [HttpPut("{groupId:guid}")]
        public async Task<IActionResult> Edit([FromRoute(Name = "groupId")] string groupId, [FromForm] CreateGroup dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var group = await _groupService.EditGroupAsync(UserId, groupId, dto);

                foreach (var participant in group.Values.First().Participants.Keys.ToList())
                {
                    await _notificationHubContext.Clients.User(participant).SendAsync("ReceiveGroupProfiles", group);
                }

                return Ok(new { message = "اطلاعات گروه به‌روزرسانی شد." });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FirebaseException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// کاربر را از گروه خارج می‌کند.
        /// </summary>
        /// <param name="groupId">شناسه گروهی که کاربر قصد خروج از آن را دارد.</param>
        /// <returns>پیام موفقیت‌آمیز بودن خروج از گروه برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت بروز خطا در درخواست، پیام خطا برمی‌گرداند.</exception>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد، پیام خطا برمی‌گرداند.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره، پیام خطا برمی‌گرداند.</exception>
        [HttpDelete("{groupId:guid}")]
        public async Task<IActionResult> Leave([FromRoute(Name = "groupId")] string groupId)
        {
            try
            {
                var group = await _groupService.LeaveGroupAsync(UserId, groupId);

                foreach (var participant in group.Values.First().Participants.Keys.ToList())
                {
                    await _notificationHubContext.Clients.User(participant).SendAsync("ReceiveGroupProfiles", group);
                }

                return Ok(new { message = "از گروه خارج شدید." });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FirebaseException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }
    }
}