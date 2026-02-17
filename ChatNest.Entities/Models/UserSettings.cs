using ChatNest.Entities.Enums;

namespace ChatNest.Entities.Models
{
    /// <summary>
    /// کلاس نمایانگر تنظیمات کاربر.
    /// تنظیمات شخصی کاربر مانند تم و پس‌زمینه گفتگو را شامل می‌شود.
    /// </summary>
    public sealed class UserSettings
    {
        public Theme Theme { get; set; } = Enums.Theme.DefaultSystemMode;

        public string ChatBackground { get; set; } = "color1";
        public bool ShowLastSeen { get; set; } = true;
        public bool ShowProfilePhoto { get; set; } = true;
        public bool ShowBiography { get; set; } = true;
        public bool NotificationEnabled { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;
        public bool OnlineStatus { get; set; } = true;
        public string Language { get; set; } = "en";
        public bool ReadReceiptEnabled { get; set; } = true;
        public bool LastSeenEnabled { get; set; } = true;
        public bool ProfilePhotoVisible { get; set; } = true;
        public bool GroupInviteEnabled { get; set; } = true;
    }
}
