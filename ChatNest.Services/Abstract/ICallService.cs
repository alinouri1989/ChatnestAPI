using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;

namespace ChatNest.Services.Abstract
{
    /// <summary>
    /// رابط سرویس مورد نیاز برای عملیات تماس بین کاربران.
    /// </summary>
    public interface ICallService
    {
        /// <summary>
        /// یک تماس شروع می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که تماس را شروع می‌کند.</param>
        /// <param name="recipientId">شناسه کاربر گیرنده‌ای که تماس با او برقرار می‌شود.</param>
        /// <param name="callType">نوع تماس (صوتی، تصویری و غیره).</param>
        /// <returns>یک عملیات ناهمزمان حاوی شناسه تماس برمی‌گرداند.</returns>
        Task<string> StartCallAsync(string userId, string recipientId, CallType callType);

        /// <summary>
        /// یک تماس را قبول می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که تماس را قبول می‌کند.</param>
        /// <param name="callId">شناسه تماسی که باید قبول شود.</param>
        /// <returns>یک عملیات ناهمزمان برمی‌گرداند.</returns>
        Task AcceptCallAsync(string userId, string callId);

        /// <summary>
        /// یک تماس را پایان می‌دهد.
        /// </summary>
        /// <param name="userId">شناسه کاربری که تماس را پایان می‌دهد.</param>
        /// <param name="callId">شناسه تماسی که باید پایان یابد.</param>
        /// <param name="callStatus">وضعیت تماس (موفق، ناموفق و غیره).</param>
        /// <param name="createdDate">تاریخ ایجاد تماس.</param>
        /// <returns>دیکشنری حاوی اطلاعات تماس برمی‌گرداند.</returns>
        Task<Dictionary<string, Call>> EndCallAsync(string userId, string callId, CallStatus callStatus, DateTime? createdDate);

        /// <summary>
        /// یک تماس را حذف می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که تماس را حذف می‌کند.</param>
        /// <param name="callId">شناسه تماسی که باید حذف شود.</param>
        /// <returns>یک عملیات ناهمزمان برمی‌گرداند.</returns>
        Task DeleteCallAsync(string userId, string callId);

        /// <summary>
        /// کاربران شرکت‌کننده در یک تماس را دریافت می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربر.</param>
        /// <param name="callId">شناسه تماسی که شرکت‌کنندگان آن دریافت می‌شود.</param>
        /// <returns>فهرستی حاوی شناسه کاربران شرکت‌کننده در تماس برمی‌گرداند.</returns>
        Task<List<string>> GetCallParticipantsAsync(string userId, string callId);

        /// <summary>
        /// سوابق تماس متعلق به کاربر را دریافت می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربر.</param>
        /// <returns>دیکشنری حاوی گزارش‌های تماس و فهرست شرکت‌کنندگان برمی‌گرداند.</returns>
        Task<(Dictionary<string, Dictionary<string, Call>>, List<string>)> GetCallLogs(string userId);

        /// <summary>
        /// اطلاعات در مورد یک تماس مشخص دریافت می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که اطلاعات تماس را دریافت می‌کند.</param>
        /// <param name="callId">شناسه تماسی که اطلاعات آن دریافت می‌شود.</param>
        /// <returns>شیء تماس حاوی اطلاعات تماس برمی‌گرداند.</returns>
        Task<Call> GetCallAsync(string userId, string callId);
    }
}