using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;

namespace ChatNest.Services.Abstract
{
    /// <summary>
    /// رابط ارائه‌دهنده سرویس‌های مدیریت کاربر.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// کاربران را جستجو می‌کند و موارد منطبق را برمی‌گرداند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که جستجو انجام می‌دهد.</param>
        /// <param name="query">عبارت جستجو.</param>
        /// <returns>دیکشنری حاوی کاربران منطبق برمی‌گرداند.</returns>
        Task<Dictionary<string, FoundUsers>> SearchUsersAsync(string userId, string query);

        /// <summary>
        /// اطلاعات کاربر را دریافت می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که اطلاعات آن دریافت می‌شود.</param>
        /// <returns>DTO حاوی اطلاعات کاربر برمی‌گرداند.</returns>
        Task<UserInfo> GetUserInfoAsync(string userId);

        /// <summary>
        /// چندین پروفایل کاربر را دریافت می‌کند.
        /// </summary>
        /// <param name="recipientIds">فهرست شناسه‌ها برای پروفایل‌های کاربر.</param>
        /// <returns>دیکشنری حاوی پروفایل‌های کاربر برمی‌گرداند.</returns>
        Task<Dictionary<string, CallerUser>> GetUserProfilesAsync(List<string> recipientIds);

        /// <summary>
        /// عکس پروفایل کاربر را حذف می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که عکس پروفایل آن حذف می‌شود.</param>
        /// <returns>URI عکس پروفایل حذف شده را برمی‌گرداند.</returns>
        Task<Uri> RemoveProfilePhotoAsync(string userId);

        /// <summary>
        /// عکس پروفایل کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که عکس پروفایل آن به‌روزرسانی می‌شود.</param>
        /// <param name="dto">DTO حاوی عکس پروفایل جدید.</param>
        /// <returns>URI عکس پروفایل به‌روزرسانی شده را برمی‌گرداند.</returns>
        Task<Uri> UpdateProfilePhotoAsync(string userId, UpdateProfilePhoto dto);

        /// <summary>
        /// نام نمایشی (DisplayName) کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که نام نمایشی آن به‌روزرسانی می‌شود.</param>
        /// <param name="dto">DTO حاوی اطلاعات نام نمایشی جدید.</param>
        /// <returns>نتیجه عملیات را برمی‌گرداند (void).</returns>
        Task UpdateDisplayNameAsync(string userId, UpdateDisplayName dto);

        /// <summary>
        /// شناسه عمومی کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="userId">شناسه داخلی کاربر.</param>
        /// <param name="dto">شناسه عمومی جدید.</param>
        Task UpdateUserIdentifierAsync(string userId, UpdateUserIdentifier dto);

        /// <summary>
        /// شماره تلفن کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که شماره تلفن آن به‌روزرسانی می‌شود.</param>
        /// <param name="dto">DTO حاوی شماره تلفن جدید.</param>
        /// <returns>نتیجه عملیات را برمی‌گرداند (void).</returns>
        Task UpdatePhoneNumberAsync(string userId, UpdatePhoneNumber dto);

        /// <summary>
        /// بیوگرافی کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که بیوگرافی آن به‌روزرسانی می‌شود.</param>
        /// <param name="dto">DTO حاوی اطلاعات بیوگرافی جدید.</param>
        /// <returns>نتیجه عملیات را برمی‌گرداند (void).</returns>
        Task UpdateBiographyAsync(string userId, UpdateBiography dto);

        /// <summary>
        /// رمز عبور کاربر را تغییر می‌دهد.
        /// </summary>
        /// <param name="userId">شناسه کاربری که رمز عبور آن تغییر می‌کند.</param>
        /// <param name="dto">DTO حاوی رمز عبور جدید.</param>
        /// <returns>نتیجه عملیات را برمی‌گرداند (void).</returns>
        Task ChangePasswordAsync(string userId, ChangePassword dto);

        /// <summary>
        /// پوسته کاربر را تغییر می‌دهد.
        /// </summary>
        /// <param name="userId">شناسه کاربری که پوسته آن تغییر می‌کند.</param>
        /// <param name="dto">DTO حاوی اطلاعات پوسته جدید.</param>
        /// <returns>نتیجه عملیات را برمی‌گرداند (void).</returns>
        Task ChangeThemeAsync(string userId, ChangeTheme dto);

        /// <summary>
        /// پس‌زمینه گفتگوی کاربر را تغییر می‌دهد.
        /// </summary>
        /// <param name="userId">شناسه کاربری که پس‌زمینه گفتگو آن تغییر می‌کند.</param>
        /// <param name="dto">DTO حاوی پس‌زمینه گفتگوی جدید.</param>
        /// <returns>نتیجه عملیات را برمی‌گرداند (void).</returns>
        Task ChangeChatBackgroundAsync(string userId, ChangeChatBackground dto);

        /// <summary>
        /// پرسش امنیتی و پاسخ آن را برای کاربر تنظیم/به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <param name="dto">اطلاعات پرسش امنیتی</param>
        Task UpdateSecurityQuestionAsync(string userId, UpdateSecurityQuestion dto);

        Task RegisterFirebaseTokenAsync(string userId, FirebaseTokenRequest dto);

        Task RemoveFirebaseTokenAsync(string userId, FirebaseTokenRequest dto);

        /// <summary>
        /// اطلاعات پروفایل چندین گیرنده را دریافت می‌کند.
        /// </summary>
        /// <param name="recipientIds">شناسه‌های گیرندگان.</param>
        /// <returns>دیکشنری حاوی پروفایل‌های گیرنده برمی‌گرداند.</returns>
        Task<Dictionary<string, RecipientProfile>> GetRecipientProfilesAsync(List<string> recipientIds);

        /// <summary>
        /// تاریخ آخرین اتصال کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که تاریخ اتصال آن به‌روزرسانی می‌شود.</param>
        /// <param name="lastConnectionDate">تاریخ اتصال جدید.</param>
        /// <returns>نتیجه عملیات را برمی‌گرداند (void).</returns>
        Task UpdateLastConnectionDateAsync(string userId, DateTime lastConnectionDate);
    }
}
