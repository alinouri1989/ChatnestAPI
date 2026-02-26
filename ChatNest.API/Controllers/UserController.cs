using ChatNest.API.Hubs;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;
using Firebase.Auth;
using Firebase.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChatNest.API.Controllers
{
    /// <summary>
    /// کنترلر API برای انجام عملیات مرتبط با کاربر.
    /// اطلاعات کاربر، عکس پروفایل، نام، شماره تلفن و سایر داده‌ها قابل به‌روزرسانی هستند.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    public sealed class UserController : BaseController
    {
        private readonly IHubContext<NotificationHub> _notificationHubContext;
        private readonly IUserService _userService;

        /// <summary>
        /// سازنده کلاس UserController.
        /// سرویس‌های مورد نیاز را دریافت کرده و کنترلر را راه‌اندازی می‌کند.
        /// </summary>
        /// <param name="notificationHubContext">شیء <see cref="IHubContext{NotificationHub}"/> برای هاب اعلان‌ها.</param>
        /// <param name="userService">شیء <see cref="IUserService"/> برای عملیات کاربر.</param>
        public UserController(IHubContext<NotificationHub> notificationHubContext, IUserService userService)
        {
            _notificationHubContext = notificationHubContext;
            _userService = userService;
        }

        /// <summary>
        /// اطلاعات کاربر را واکشی می‌کند.
        /// اطلاعات کاربر بر اساس شناسه کاربری دریافت شده و برگردانده می‌شود.
        /// </summary>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی اطلاعات کاربر است.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن کاربر پرتاب می‌شود.</exception>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpGet]
        public async Task<IActionResult> Info()
        {
            try
            {
                return Ok(await _userService.GetUserInfoAsync(UserId));
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
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
        /// عکس پروفایل کاربر را حذف می‌کند.
        /// پس از حذف عکس پروفایل، تغییرات مربوطه به تمام کلاینت‌ها اطلاع داده می‌شود.
        /// </summary>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حذف عکس پروفایل را اطلاع می‌دهد.</returns>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpDelete]
        public async Task<IActionResult> ProfilePhoto()
        {
            try
            {
                var profilePhoto = await _userService.RemoveProfilePhotoAsync(UserId);
                await _notificationHubContext.Clients.All.SendAsync("ReceiveRecipientProfiles", new Dictionary<string, Dictionary<string, object>> { { UserId, new Dictionary<string, object> { { "profilePhoto", profilePhoto } } } });

                return Ok(new { message = "عکس پروفایل حذف شد.", profilePhoto });
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
        /// عکس پروفایل کاربر را به‌روزرسانی می‌کند.
        /// عکس پروفایل جدید با موفقیت به‌روزرسانی شده و تغییرات به تمام کلاینت‌ها اطلاع داده می‌شود.
        /// </summary>
        /// <param name="dto">شیء <see cref="UpdateProfilePhoto"/> حاوی اطلاعات به‌روزرسانی عکس پروفایل.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی عکس پروفایل جدید است.</returns>
        /// <exception cref="BadRequestException">در صورت نامعتبر بودن داده‌های ارسالی پرتاب می‌شود.</exception>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPatch]
        public async Task<IActionResult> ProfilePhoto([FromBody] UpdateProfilePhoto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var profilePhoto = await _userService.UpdateProfilePhotoAsync(UserId, dto);
                await _notificationHubContext.Clients.All.SendAsync("ReceiveRecipientProfiles", new Dictionary<string, Dictionary<string, object>> { { UserId, new Dictionary<string, object> { { "profilePhoto", profilePhoto } } } });

                return Ok(new { message = "عکس پروفایل به‌روزرسانی شد.", profilePhoto });
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
        /// نام نمایشی کاربر (displayName) به‌روزرسانی می‌شود.
        /// نام کاربری جدید با موفقیت به‌روزرسانی شده و تغییرات به تمام کلاینت‌ها اطلاع داده می‌شود.
        /// </summary>
        /// <param name="dto">شیء <see cref="UpdateDisplayName"/> حاوی اطلاعات به‌روزرسانی نام کاربری.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی اطلاعات نام کاربری جدید است.</returns>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPatch]
        public async Task<IActionResult> DisplayName([FromBody] UpdateDisplayName dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _userService.UpdateDisplayNameAsync(UserId, dto);
                await _notificationHubContext.Clients.All.SendAsync("ReceiveRecipientProfiles", new Dictionary<string, Dictionary<string, object>> { { UserId, new Dictionary<string, object> { { "displayName", dto.DisplayName } } } });

                return Ok(new { message = "نام کاربری به‌روزرسانی شد." });
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
        /// شناسه عمومی کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        [HttpPatch]
        public async Task<IActionResult> UserIdentifier([FromBody] UpdateUserIdentifier dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _userService.UpdateUserIdentifierAsync(UserId, dto);
                await _notificationHubContext.Clients.All.SendAsync(
                    "ReceiveRecipientProfiles",
                    new Dictionary<string, Dictionary<string, object>>
                    {
                        { UserId, new Dictionary<string, object> { { "userIdentifier", dto.UserIdentifier } } }
                    });

                return Ok(new { message = "شناسه کاربر به‌روزرسانی شد." });
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
        /// شماره تلفن کاربر را به‌روزرسانی می‌کند.
        /// شماره تلفن جدید با موفقیت به‌روزرسانی شده و تغییرات به تمام کلاینت‌ها اطلاع داده می‌شود.
        /// </summary>
        /// <param name="dto">شیء <see cref="UpdatePhoneNumber"/> حاوی اطلاعات به‌روزرسانی شماره تلفن.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی اطلاعات شماره تلفن جدید است.</returns>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPatch]
        public async Task<IActionResult> PhoneNumber([FromBody] UpdatePhoneNumber dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _userService.UpdatePhoneNumberAsync(UserId, dto);
                await _notificationHubContext.Clients.All.SendAsync("ReceiveRecipientProfiles", new Dictionary<string, Dictionary<string, object>> { { UserId, new Dictionary<string, object> { { "phoneNumber", dto.PhoneNumber } } } });

                return Ok(new { message = "شماره تلفن به‌روزرسانی شد." });
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
        /// بیوگرافی کاربر را به‌روزرسانی می‌کند.
        /// بیوگرافی جدید با موفقیت به‌روزرسانی شده و تغییرات به تمام کلاینت‌ها اطلاع داده می‌شود.
        /// </summary>
        /// <param name="dto">شیء <see cref="UpdateBiography"/> حاوی اطلاعات به‌روزرسانی بیوگرافی.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی اطلاعات بیوگرافی جدید است.</returns>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPatch]
        public async Task<IActionResult> Biography([FromBody] UpdateBiography dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _userService.UpdateBiographyAsync(UserId, dto);
                await _notificationHubContext.Clients.All.SendAsync("ReceiveRecipientProfiles", new Dictionary<string, Dictionary<string, object>> { { UserId, new Dictionary<string, object> { { "biography", dto.Biography } } } });

                return Ok(new { message = "بیوگرافی به‌روزرسانی شد." });
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
        /// رمز عبور کاربر را تغییر می‌دهد.
        /// رمز عبور جدید با موفقیت به‌روزرسانی می‌شود.
        /// </summary>
        /// <param name="dto">شیء <see cref="ChangePassword"/> حاوی اطلاعات تغییر رمز عبور.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی پیام عملیات است.</returns>
        /// <exception cref="FirebaseAuthHttpException">در صورت اطلاعات نامعتبر، تلاش‌های زیاد یا مسدود بودن کاربر، پیام‌های خطای مرتبط برگردانده می‌شود.</exception>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPatch]
        public async Task<IActionResult> Password([FromBody] ChangePassword dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _userService.ChangePasswordAsync(UserId, dto);
                return Ok(new { message = "رمز عبور تغییر یافت." });
            }
            catch (FirebaseAuthHttpException ex)
            {
                if (ex.Message.Contains("INVALID_LOGIN_CREDENTIALS"))
                {
                    return Unauthorized(new { message = "رمز عبور فعلی شما اشتباه است.", errorDetails = ex.Message });
                }
                return ex.Reason switch
                {
                    AuthErrorReason.TooManyAttemptsTryLater => StatusCode(StatusCodes.Status403Forbidden, new { message = "تلاش‌های زیادی برای ورود انجام شده. لطفاً بعداً دوباره تلاش کنید.", errorDetails = ex.Message }),
                    AuthErrorReason.OperationNotAllowed => StatusCode(StatusCodes.Status403Forbidden, new { message = "این عملیات در حال حاضر مجاز نیست.", errorDetails = ex.Message }),
                    AuthErrorReason.UserDisabled => StatusCode(StatusCodes.Status403Forbidden, new { message = "حساب کاربری شما غیرفعال شده است.", errorDetails = ex.Message }),
                    _ => StatusCode(500, new { message = "خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message })
                };
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

        [HttpPatch]
        public async Task<IActionResult> SecurityQuestion([FromBody] UpdateSecurityQuestion dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _userService.UpdateSecurityQuestionAsync(UserId, dto);
                return Ok(new { message = "پرسش امنیتی با موفقیت به‌روزرسانی شد." });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// تنظیمات تم کاربر را به‌روزرسانی می‌کند.
        /// تم جدید با موفقیت به‌روزرسانی می‌شود.
        /// </summary>
        /// <param name="dto">شیء <see cref="ChangeTheme"/> حاوی اطلاعات تغییر تم.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی پیام عملیات است.</returns>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPatch]
        public async Task<IActionResult> Theme([FromBody] ChangeTheme dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _userService.ChangeThemeAsync(UserId, dto);
                return Ok(new { message = "تم به‌روزرسانی شد." });
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
        /// پس‌زمینه گفتگوی کاربر را به‌روزرسانی می‌کند.
        /// پس‌زمینه گفتگوی جدید با موفقیت به‌روزرسانی می‌شود.
        /// </summary>
        /// <param name="dto">شیء <see cref="ChangeChatBackground"/> حاوی اطلاعات تغییر پس‌زمینه گفتگو.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند که در صورت موفقیت حاوی پیام عملیات است.</returns>
        /// <exception cref="FirebaseException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPatch]
        public async Task<IActionResult> ChatBackground([FromBody] ChangeChatBackground dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _userService.ChangeChatBackgroundAsync(UserId, dto);
                return Ok(new { message = "پس‌زمینه گفتگو به‌روزرسانی شد." });
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
