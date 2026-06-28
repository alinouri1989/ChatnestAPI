namespace ChatNest.Entities.Enums
{
    /// <summary>
    /// enum نمایانگر محتوای پیام.
    /// نوع محتوای پیام را مشخص می‌کند (متن، تصویر، ویدیو، صدا، فایل).
    /// </summary>
    public enum MessageContent
    {
        Text,
        Image,
        Video,
        Audio,
        File,
        Location
    }
}
