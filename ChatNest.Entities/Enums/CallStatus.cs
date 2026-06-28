namespace ChatNest.Entities.Enums
{
    /// <summary>
    /// enum نمایانگر وضعیت‌های تماس.
    /// وضعیت فعلی تماس را مشخص می‌کند (در انتظار، پذیرفته‌شده، ردشده، لغوشده، بی‌پاسخ).
    /// </summary>
    public enum CallStatus
    {
        Pending,
        Accepted,
        Declined,
        Canceled,
        Missed,
        Ongoing
    }
}
