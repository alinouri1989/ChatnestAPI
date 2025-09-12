using ChatNest.Entities.Models;

namespace ChatNest.Services.Abstract
{
    /// <summary>
    /// رابط سرویس مورد نیاز برای عملیات گفتگو بین کاربران.
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// یک گفتگوی جدید ایجاد می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که گفتگو را شروع می‌کند.</param>
        /// <param name="chatType">نوع گفتگو (مثال: خصوصی، گروهی).</param>
        /// <param name="recipientId">شناسه کاربر گیرنده‌ای که در گفتگو شرکت خواهد کرد.</param>
        /// <returns>دیکشنری حاوی گفتگوی جدید ایجاد شده برمی‌گرداند.</returns>
        Task<Dictionary<string, Chat>> CreateChatAsync(string userId, string chatType, string recipientId);

        /// <summary>
        /// تمام گفتگوهای کاربر را دریافت می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربر.</param>
        /// <returns>اطلاعاتی در مورد تمام گفتگوهای کاربر، گفتگوهای آرشیو شده و حذف شده برمی‌گرداند.</returns>
        Task<(Dictionary<string, Dictionary<string, Chat>>, List<string>, List<string>)> GetAllChatsAsync(string userId);

        /// <summary>
        /// یک گفتگو را پاک می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که گفتگو را پاک خواهد کرد.</param>
        /// <param name="chatType">نوع گفتگویی که باید پاک شود.</param>
        /// <param name="chatId">شناسه گفتگویی که باید پاک شود.</param>
        /// <returns>دیکشنری حاوی گفتگوی پاک شده برمی‌گرداند.</returns>
        Task<Dictionary<string, Dictionary<string, Chat>>> ClearChatAsync(string userId, string chatType, string chatId);

        /// <summary>
        /// یک گفتگوی شخصی را آرشیو می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که گفتگو را آرشیو خواهد کرد.</param>
        /// <param name="chatId">شناسه گفتگویی که باید آرشیو شود.</param>
        /// <returns>دیکشنری حاوی اطلاعات گفتگوی آرشیو شده برمی‌گرداند.</returns>
        Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> ArchiveIndividualChatAsync(string userId, string chatId);

        /// <summary>
        /// یک گفتگوی شخصی را از آرشیو خارج می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که گفتگو را از آرشیو خارج خواهد کرد.</param>
        /// <param name="chatId">شناسه گفتگویی که باید از آرشیو خارج شود.</param>
        /// <returns>دیکشنری حاوی اطلاعات گفتگوی خارج شده از آرشیو برمی‌گرداند.</returns>
        Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> UnarchiveIndividualChatAsync(string userId, string chatId);
    }
}