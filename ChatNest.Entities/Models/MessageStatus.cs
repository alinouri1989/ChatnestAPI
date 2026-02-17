namespace ChatNest.Entities.Models
{
    /// <summary>
    /// کلاسی که وضعیت‌های ارسال، تحویل و خواندن پیام را نگه می‌دارد.
    /// برای هر وضعیت، اطلاعات تاریخ مرتبط را شامل می‌شود.
    /// </summary>
    public class MessageStatus
    {
        public Dictionary<string, DateTime> Sent { get; set; } = [];

        public Dictionary<string, DateTime> Delivered { get; set; } = [];

        public Dictionary<string, DateTime> Read { get; set; } = [];
    }
}
