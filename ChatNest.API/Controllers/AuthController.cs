using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;
using Firebase.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNest.API.Controllers
{
    /// <summary>
    /// کلاس کنترلر API که عملیات احراز هویت کاربر را مدیریت می‌کند.
    /// ثبت نام کاربر، ورود به سیستم، و ورود از طریق شبکه‌های اجتماعی را مدیریت می‌کند.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        /// <summary>
        /// یک نمونه جدید از کلاس <see cref="AuthController"/> را ایجاد می‌کند.
        /// </summary>
        /// <param name="authService">وابستگی <see cref="IAuthService"/> برای عملیات احراز هویت کاربر.</param>
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// ثبت نام کاربر را انجام می‌دهد.
        /// در صورت وجود ورودی‌های نامعتبر و شرایط خطا، پاسخ‌های مناسب برمی‌گرداند.
        /// </summary>
        /// <param name="dto">شیء انتقال داده <see cref="SignUp"/> که اطلاعات کاربر مورد نیاز برای ثبت نام را شامل می‌شود.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت عملیات ثبت نام نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="FirebaseAuthHttpException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        public async Task<IActionResult> SignUp([FromBody] SignUp dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _authService.SignUpAsync(dto);
                return Ok(new { message = "کاربر با موفقیت ثبت شد." });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FirebaseAuthHttpException ex)
            {
                return ex.Reason switch
                {
                    AuthErrorReason.EmailExists => Conflict(new { message = "این آدرس ایمیل قبلاً استفاده شده است.", errorDetails = ex.Message }),
                    AuthErrorReason.OperationNotAllowed => StatusCode(StatusCodes.Status403Forbidden, new { message = "این عملیات در حال حاضر مجاز نیست.", errorDetails = ex.Message }),
                    AuthErrorReason.TooManyAttemptsTryLater => StatusCode(StatusCodes.Status403Forbidden, new { message = "تلاش‌های زیادی برای ثبت نام انجام شده. لطفاً بعداً دوباره تلاش کنید.", errorDetails = ex.Message }),
                    _ => StatusCode(500, new { message = $"خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// ورود به سیستم با ایمیل و رمز عبور را انجام می‌دهد.
        /// در صورت اطلاعات هویتی نامعتبر و شرایط خطا، پاسخ‌های مناسب برمی‌گرداند.
        /// </summary>
        /// <param name="dto">شیء انتقال داده <see cref="SignInEmail"/> که اطلاعات ایمیل و رمز عبور مورد نیاز برای ورود را شامل می‌شود.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند.</returns>
        /// <exception cref="FirebaseAuthHttpException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        public async Task<IActionResult> SignInEmail([FromBody] SignInEmail dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                return Ok(new { token = await _authService.SignInEmailAsync(dto) });
            }
            catch (FirebaseAuthHttpException ex)
            {
                if (ex.Message.Contains("INVALID_LOGIN_CREDENTIALS"))
                {
                    return Unauthorized(new { message = "ایمیل یا رمز عبور اشتباه است.", errorDetails = ex.Message });
                }
                return ex.Reason switch
                {
                    AuthErrorReason.TooManyAttemptsTryLater => StatusCode(StatusCodes.Status403Forbidden, new { message = "تلاش‌های زیادی برای ورود انجام شده. لطفاً بعداً دوباره تلاش کنید.", errorDetails = ex.Message }),
                    AuthErrorReason.OperationNotAllowed => StatusCode(StatusCodes.Status403Forbidden, new { message = "این عملیات در حال حاضر مجاز نیست.", errorDetails = ex.Message }),
                    AuthErrorReason.UserDisabled => StatusCode(StatusCodes.Status403Forbidden, new { message = "حساب کاربری شما غیرفعال شده است.", errorDetails = ex.Message }),
                    _ => StatusCode(500, new { message = $"خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// ورود به سیستم با Google را انجام می‌دهد.
        /// در صورت نامعتبر بودن ورود Google و سایر شرایط خطا، پاسخ‌های مناسب برمی‌گرداند.
        /// </summary>
        /// <param name="dto">شیء انتقال داده <see cref="SignInGoogle"/> که اطلاعات مورد نیاز برای ورود با Google را شامل می‌شود.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت عملیات ورود نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="FirebaseAuthHttpException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        public async Task<IActionResult> SignInGoogle([FromBody] SignInProvider dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                return Ok(new { token = await _authService.SignInGoogleAsync(dto) });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FirebaseAuthHttpException ex)
            {
                return ex.Reason switch
                {
                    AuthErrorReason.TooManyAttemptsTryLater => StatusCode(StatusCodes.Status403Forbidden, new { message = "تلاش‌های زیادی برای ورود انجام شده. لطفاً بعداً دوباره تلاش کنید.", errorDetails = ex.Message }),
                    AuthErrorReason.OperationNotAllowed => StatusCode(StatusCodes.Status403Forbidden, new { message = "این عملیات در حال حاضر مجاز نیست.", errorDetails = ex.Message }),
                    AuthErrorReason.UserDisabled => StatusCode(StatusCodes.Status403Forbidden, new { message = "حساب کاربری شما غیرفعال شده است.", errorDetails = ex.Message }),
                    _ => StatusCode(500, new { message = $"خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// ورود به سیستم با Facebook را انجام می‌دهد.
        /// در صورت نامعتبر بودن ورود Facebook و سایر شرایط خطا، پاسخ‌های مناسب برمی‌گرداند.
        /// </summary>
        /// <param name="dto">شیء انتقال داده <see cref="SignInProvider"/> که اطلاعات مورد نیاز برای ورود با Facebook را شامل می‌شود.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت عملیات ورود نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="FirebaseAuthHttpException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        public async Task<IActionResult> SignInFacebook([FromBody] SignInProvider dto)
        {
            try
            {
                return Ok(new { token = await _authService.SignInFacebookAsync(dto) });
            }
            catch (FirebaseAuthHttpException ex)
            {
                return ex.Reason switch
                {
                    AuthErrorReason.TooManyAttemptsTryLater => StatusCode(StatusCodes.Status403Forbidden, new { message = "تلاش‌های زیادی برای ورود انجام شده. لطفاً بعداً دوباره تلاش کنید.", errorDetails = ex.Message }),
                    AuthErrorReason.OperationNotAllowed => StatusCode(StatusCodes.Status403Forbidden, new { message = "این عملیات در حال حاضر مجاز نیست.", errorDetails = ex.Message }),
                    AuthErrorReason.UserDisabled => StatusCode(StatusCodes.Status403Forbidden, new { message = "حساب کاربری شما غیرفعال شده است.", errorDetails = ex.Message }),
                    _ => StatusCode(500, new { message = $"خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// کاربر را از سیستم خارج می‌کند.
        /// در صورت بروز خطای مرتبط با Firebase، پاسخ مناسب برمی‌گرداند.
        /// </summary>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند.</returns>
        /// <exception cref="FirebaseAuthHttpException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SignOut()
        {
            try
            {
                return Ok(new { message = "از سیستم خارج شدید." });
            }
            catch (FirebaseAuthHttpException ex)
            {
                return StatusCode(500, new { message = $"خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// لینک بازیابی رمز عبور ارسال می‌کند.
        /// در صورت ایمیل نامعتبر یا شرایط خطا، پاسخ‌های مناسب برمی‌گرداند.
        /// </summary>
        /// <param name="email">آدرس ایمیل کاربری که رمز عبورش باید بازیابی شود.</param>
        /// <returns>یک <see cref="IActionResult"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن آدرس ایمیل پرتاب می‌شود.</exception>
        /// <exception cref="FirebaseAuthHttpException">زمانی که خطایی مرتبط با Firebase رخ دهد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        public async Task<IActionResult> Password([FromBody] string email)
        {
            try
            {
                await _authService.ResetPasswordAsync(email);
                return Ok(new { message = "لینک بازیابی رمز عبور ارسال شد." });
            }
            catch (NotFoundException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FirebaseAuthHttpException ex)
            {
                return StatusCode(500, new { message = $"خطایی مرتبط با Firebase رخ داده است!", errorDetails = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }
    }
}