using ChatNest.Shared.DTOs.Request;
using Microsoft.AspNetCore.Identity;

namespace ChatNest.Services.Abstract
{
    /// <summary>
    /// رابط سرویس مورد نیاز برای عملیات احراز هویت کاربر.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// یک کاربر جدید ثبت می‌کند.
        /// </summary>
        /// <param name="dto">DTO حاوی اطلاعات مورد نیاز برای ثبت کاربر</param>
        /// <returns>یک عملیات ناهمزمان برمی‌گرداند.</returns>
        Task<IdentityResult> SignUpAsync(SignUp dto);

        /// <summary>
        /// ورود کاربر با ایمیل را انجام می‌دهد.
        /// </summary>
        /// <param name="dto">DTO حاوی اطلاعات ایمیل و رمز عبور</param>
        /// <returns>توکن احراز هویت را برمی‌گرداند.</returns>
        Task<string> SignInEmailAsync(SignInEmail dto);

        /// <summary>
        /// ورود کاربر از طریق Google را انجام می‌دهد.
        /// </summary>
        /// <param name="dto">DTO حاوی اطلاعات ورود Google</param>
        /// <returns>توکن احراز هویت را برمی‌گرداند.</returns>
        Task<string> SignInGoogleAsync(SignInProvider dto);

        /// <summary>
        /// ورود کاربر از طریق Facebook را انجام می‌دهد.
        /// </summary>
        /// <param name="dto">DTO حاوی اطلاعات ورود Facebook</param>
        /// <returns>توکن احراز هویت را برمی‌گرداند.</returns>
        Task<string> SignInFacebookAsync(SignInProvider dto);

        /// <summary>
        /// درخواست بازنشانی رمز عبور ایجاد می‌کند.
        /// </summary>
        /// <param name="email">آدرس ایمیل استفاده شده برای درخواست بازنشانی رمز عبور</param>
        /// <returns>یک عملیات ناهمزمان برمی‌گرداند.</returns>
        Task ResetPasswordAsync(string email);

        /// <summary>
        /// بازنشانی رمز عبور را با استفاده از توکن ارسال‌شده در ایمیل تکمیل می‌کند.
        /// </summary>
        /// <param name="dto">DTO حاوی ایمیل، توکن و رمز عبور جدید</param>
        /// <returns>یک عملیات ناهمزمان برمی‌گرداند.</returns>
        Task ConfirmResetPasswordAsync(ResetPasswordConfirm dto);
    }
}
