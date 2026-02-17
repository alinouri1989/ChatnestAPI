using Microsoft.Extensions.Configuration;
using ChatNest.Core.Abstract;
using ChatNest.Shared.DTOs.Request;

namespace ChatNest.Core.Concrete
{
    /// <summary>
    /// کلاس موردنیاز برای فرایندهای اعتبارسنجی کاربر.
    /// این کلاس ورود از طریق ارائه‌دهنده‌هایی مانند Google و Facebook را اعتبارسنجی می‌کند.
    /// </summary>
    /// <remarks>
    /// این کلاس برای اعتبارسنجی کلید API فایربیس و بررسی زمان انقضای نشست ارائه‌دهنده‌ها استفاده می‌شود.
    /// </remarks>
    public class AuthManager : IAuthManager
    {
        private readonly string _apiKey;



        /// <summary>
        /// سازنده کلاس <see cref="AuthManager"/> است.
        /// کلید API فایربیس را از فایل پیکربندی دریافت می‌کند.
        /// </summary>
        /// <param name="configuration">شیء <see cref="IConfiguration"/> شامل تنظیمات پیکربندی.</param>
        public AuthManager(IConfiguration configuration)
        {
            _apiKey = configuration["Firebase:apiKey"]!;
        }



        /// <summary>
        /// فرایند ورود از طریق ارائه‌دهنده Google را اعتبارسنجی می‌کند.
        /// </summary>
        /// <param name="dto">شیء <see cref="SignInProvider"/> شامل اطلاعات ارائه‌دهنده.</param>
        /// <returns>تاپل شامل نتیجه اعتبارسنجی و پیام خطا را برمی‌گرداند. اگر IsValid = false باشد، پیام خطا دارد.</returns>
        /// <remarks>
        /// این متد داده‌های ارائه‌دهنده Google را بررسی کرده و معتبر بودن آن‌ها را ارزیابی می‌کند.
        /// همچنین اعتبار کلید API و منقضی نشدن زمان نشست را کنترل می‌کند.
        /// </remarks>
        public (bool IsValid, string ErrorMessage) ValidateGoogleProvider(SignInProvider dto)
        {
            if (!dto.ProviderData[0].ProviderId.Equals("google.com"))
            {
                return (false, "ارائه‌دهنده نامعتبر است. حساب Google الزامی است.");
            }

            if (!dto.apiKey.Equals(_apiKey))
            {
                return (false, "کلید API نامعتبر است.");
            }

            var expirationTime = dto.StsTokenManager.ExpirationTime;
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (expirationTime < currentTime)
            {
                return (false, "نشست منقضی شده است.");
            }

            return (true, "");
        }



        /// <summary>
        /// فرایند ورود از طریق ارائه‌دهنده Facebook را اعتبارسنجی می‌کند.
        /// </summary>
        /// <param name="dto">شیء <see cref="SignInProvider"/> شامل اطلاعات ارائه‌دهنده.</param>
        /// <returns>تاپل شامل نتیجه اعتبارسنجی و پیام خطا را برمی‌گرداند. اگر IsValid = false باشد، پیام خطا دارد.</returns>
        /// <remarks>
        /// این متد داده‌های ارائه‌دهنده Facebook را بررسی کرده و معتبر بودن آن‌ها را ارزیابی می‌کند.
        /// همچنین اعتبار کلید API و منقضی نشدن زمان نشست را کنترل می‌کند.
        /// </remarks>
        public (bool IsValid, string ErrorMessage) ValidateFacebookProvider(SignInProvider dto)
        {
            if (!dto.ProviderData[0].ProviderId.Equals("facebook.com"))
            {
                return (false, "ارائه‌دهنده نامعتبر است. حساب Facebook الزامی است.");
            }

            if (!dto.apiKey.Equals(_apiKey))
            {
                return (false, "کلید API نامعتبر است.");
            }

            var expirationTime = dto.StsTokenManager.ExpirationTime;
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (expirationTime < currentTime)
            {
                return (false, "نشست منقضی شده است.");
            }

            return (true, "");
        }
    }
}
