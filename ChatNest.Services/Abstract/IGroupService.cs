using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;

namespace ChatNest.Services.Abstract
{
    /// <summary>
    /// رابط ارائه‌دهنده سرویس‌های مدیریت گروه.
    /// </summary>
    public interface IGroupService
    {
        /// <summary>
        /// یک گروه جدید ایجاد می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که گروه را ایجاد می‌کند.</param>
        /// <param name="dto">DTO حاوی داده‌های مورد نیاز برای عملیات ایجاد گروه.</param>
        /// <returns>اطلاعات پروفایل گروه ایجاد شده را برمی‌گرداند.</returns>
        Task<Dictionary<string, GroupProfile>> CreateGroupAsync(string userId, CreateGroup dto);

        /// <summary>
        /// یک گروه موجود را ویرایش می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که گروه را ویرایش می‌کند.</param>
        /// <param name="groupId">شناسه گروهی که باید ویرایش شود.</param>
        /// <param name="dto">DTO حاوی داده‌های مورد نیاز برای عملیات ویرایش گروه.</param>
        /// <returns>اطلاعات پروفایل گروه ویرایش شده را برمی‌گرداند.</returns>
        Task<Dictionary<string, GroupProfile>> EditGroupAsync(string userId, string groupId, CreateGroup dto);

        /// <summary>
        /// اطلاعات پروفایل یک گروه را دریافت می‌کند.
        /// </summary>
        /// <param name="userGroupIds">شناسه گروه‌هایی که کاربر عضو آن‌هاست.</param>
        /// <returns>دیکشنری حاوی اطلاعات پروفایل گروه‌ها برمی‌گرداند.</returns>
        Task<Dictionary<string, GroupProfile>> GetGroupProfilesAsync(List<string> userGroupIds);

        /// <summary>
        /// شناسه شرکت‌کنندگان یک گروه را دریافت می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که شرکت‌کنندگان گروه را جستجو می‌کند.</param>
        /// <param name="groupId">شناسه گروهی که شرکت‌کنندگان آن جستجو می‌شود.</param>
        /// <returns>فهرستی حاوی شناسه شرکت‌کنندگان گروه برمی‌گرداند.</returns>
        Task<List<string>> GetGroupParticipantsAsync(string userId, string groupId);

        /// <summary>
        /// کاربری را از گروه خارج می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که گروه را ترک می‌کند.</param>
        /// <param name="groupId">شناسه گروهی که ترک می‌شود.</param>
        /// <returns>اطلاعات پروفایل گروه به‌روزرسانی شده پس از ترک گروه را برمی‌گرداند.</returns>
        Task<Dictionary<string, GroupProfile>> LeaveGroupAsync(string userId, string groupId);
    }
}