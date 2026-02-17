using ChatNest.Shared.DTOs.Request;

namespace ChatNest.Core.Abstract
{
    /// <summary>
    /// رابطی که قرارداد عملیات اعتبارسنجی کاربر را ارائه می‌دهد.
    /// این رابط اعتبارسنجی ورود برای ارائه‌دهنده‌های مختلف را فراهم می‌کند.
    /// </summary>
    public interface IAuthManager
    {
        /// <summary>
        /// ورود از طریق ارائه‌دهنده Google را اعتبارسنجی می‌کند.
        /// </summary>
        /// <param name="dto">شیء <see cref="SignInProvider"/> شامل اطلاعات ارائه‌دهنده.</param>
        /// <returns>تاپل شامل نتیجه اعتبارسنجی و پیام خطا را برمی‌گرداند. اگر IsValid = false باشد، پیام خطا دارد.</returns>
        (bool IsValid, string ErrorMessage) ValidateGoogleProvider(SignInProvider dto);



        /// <summary>
        /// ورود از طریق ارائه‌دهنده Facebook را اعتبارسنجی می‌کند.
        /// </summary>
        /// <param name="dto">شیء <see cref="SignInProvider"/> شامل اطلاعات ارائه‌دهنده.</param>
        /// <returns>تاپل شامل نتیجه اعتبارسنجی و پیام خطا را برمی‌گرداند. اگر IsValid = false باشد، پیام خطا دارد.</returns>
        (bool IsValid, string ErrorMessage) ValidateFacebookProvider(SignInProvider dto);
    }
}
