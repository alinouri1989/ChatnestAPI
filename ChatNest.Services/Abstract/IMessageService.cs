using ChatNest.Entities.Models;
using ChatNest.Shared.DTOs.Request;

namespace ChatNest.Services.Abstract
{
    /// <summary>
    /// رابط ارائه‌دهنده سرویس‌های مدیریت پیام.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// یک پیام ارسال می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که پیام را ارسال می‌کند.</param>
        /// <param name="chatId">شناسه گفتگویی که پیام به آن ارسال می‌شود.</param>
        /// <param name="chatType">نوع گفتگو (مثال: شخصی، گروهی).</param>
        /// <param name="dto">DTO حاوی محتوای پیام ارسالی.</param>
        /// <returns>دیکشنری حاوی پیام ارسال شده و اطلاعات مرتبط برمی‌گرداند.</returns>
        Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> SendMessageAsync(string userId, string chatId, string chatType, SendMessage dto);

        /// <summary>
        /// یک پیام را حذف می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که پیام را حذف می‌کند.</param>
        /// <param name="chatType">نوع گفتگو (مثال: شخصی، گروهی).</param>
        /// <param name="chatId">شناسه گفتگویی که پیام از آن حذف می‌شود.</param>
        /// <param name="messageId">شناسه پیامی که باید حذف شود.</param>
        /// <param name="deletionType">نوع حذف پیام (مثال: آیا فقط برای کاربر حذف شود یا برای کل گروه).</param>
        /// <returns>دیکشنری حاوی اطلاعات به‌روزرسانی شده گفتگو مرتبط با پیام حذف شده برمی‌گرداند.</returns>
        Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> DeleteMessageAsync(string userId, string chatType, string chatId, string messageId, byte deletionType);

        /// <summary>
        /// تحویل یا خوانده شدن یک پیام را علامت‌گذاری می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که پیام را می‌خواند یا تحویل می‌گیرد.</param>
        /// <param name="chatType">نوع گفتگو (مثال: شخصی، گروهی).</param>
        /// <param name="chatId">شناسه گفتگویی که پیام در آن قرار دارد.</param>
        /// <param name="messageId">شناسه پیامی که باید علامت‌گذاری شود.</param>
        /// <param name="fieldName">کدام فیلد (خوانده شده، تحویل داده شده و غیره) باید به‌روزرسانی شود.</param>
        /// <returns>دیکشنری همراه با اطلاعات به‌روزرسانی شده گفتگو برمی‌گرداند.</returns>
        Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> DeliverOrReadMessageAsync(string userId, string chatType, string chatId, string messageId, string fieldName);
    }
}