using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    /// <summary>
    /// شیء انتقال داده (DTO) برای به‌روزرسانی عکس پروفایل کاربران.
    /// </summary>
    public sealed record UpdateProfilePhoto
    {
        [Required(ErrorMessage = "لطفاً یک عکس آپلود کنید.")]
        public string ProfilePhoto { get; init; }
        public byte[] ProfilePhotoAsBytes { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// شیء انتقال داده (DTO) برای به‌روزرسانی نام و نام خانوادگی کاربران.
    /// </summary>
    public sealed record UpdateDisplayName
    {
        [Required(ErrorMessage = "لطفاً نام و نام خانوادگی خود را وارد کنید.")]
        [StringLength(50, MinimumLength = 5, ErrorMessage = "نام و نام خانوادگی شما باید حداقل 5 و حداکثر 50 کاراکتر باشد.")]
        [RegularExpression(@"^(?=.*[\u0600-\u06FFA-Za-z0-9])[\u0600-\u06FFA-Za-z0-9 ]+$", ErrorMessage = "نام نمایشی فقط می‌تواند شامل حروف فارسی/انگلیسی، عدد و فاصله باشد.")]
        public string DisplayName { get; init; }
    }

    /// <summary>
    /// شیء انتقال داده (DTO) برای به‌روزرسانی شناسه عمومی کاربر.
    /// </summary>
    public sealed record UpdateUserIdentifier
    {
        [Required(ErrorMessage = "لطفاً شناسه کاربر را وارد کنید.")]
        [StringLength(30, MinimumLength = 4, ErrorMessage = "شناسه کاربر باید حداقل 4 و حداکثر 30 کاراکتر باشد.")]
        [RegularExpression(@"^[A-Za-z0-9_]+$", ErrorMessage = "شناسه کاربر فقط می‌تواند شامل حروف انگلیسی، عدد و _ باشد.")]
        public string UserIdentifier { get; init; }
    }

    /// <summary>
    /// شیء انتقال داده (DTO) برای به‌روزرسانی شماره تلفن کاربران.
    /// </summary>
    public sealed record UpdatePhoneNumber
    {
        [Required(ErrorMessage = "لطفاً شماره تلفن خود را وارد کنید.")]
        [StringLength(15, MinimumLength = 8, ErrorMessage = "شماره تلفن باید حداقل 8 و حداکثر 15 رقم باشد.")]
        [RegularExpression(@"^(\+98|0)?9\d{9}$", ErrorMessage = "لطفاً شماره موبایل ایرانی معتبری وارد کنید (مثال: 09123456789).")]
        public string PhoneNumber { get; init; }
    }

    /// <summary>
    /// شیء انتقال داده (DTO) برای به‌روزرسانی اطلاعات بیوگرافی کاربران.
    /// </summary>
    public sealed record UpdateBiography
    {
        [Required(ErrorMessage = "لطفاً بیوگرافی خود را وارد کنید.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "بیوگرافی شما باید حداقل 1 و حداکثر 100 کاراکتر باشد.")]
        [RegularExpression(@"^[\u0600-\u06FFA-Za-z0-9\s.,!?؟،۔@#$%&*()_+=\-\[\]{}|;:""'<>/\\~`]*$", ErrorMessage = "بیوگرافی شما شامل کاراکترهای غیرمجاز است.")]
        public string Biography { get; init; }
    }

    /// <summary>
    /// شیء انتقال داده (DTO) برای تغییر رمز عبور فعلی کاربران و تعیین رمز عبور جدید.
    /// </summary>
    public sealed record ChangePassword
    {
        [Required(ErrorMessage = "لطفاً رمز عبور فعلی خود را وارد کنید.")]
        public string CurrentPassword { get; init; }

        [Required(ErrorMessage = "لطفاً رمز عبور جدید خود را وارد کنید.")]
        [StringLength(16, MinimumLength = 8, ErrorMessage = "رمز عبور شما باید حداقل 8 و حداکثر 16 کاراکتر باشد.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "رمز عبور شما باید حداقل شامل یک حرف بزرگ و یک عدد باشد.")]
        public string NewPassword { get; init; }

        [Required(ErrorMessage = "لطفاً رمز عبور جدید خود را دوباره وارد کنید.")]
        [Compare("NewPassword", ErrorMessage = "رمزهای عبور مطابقت ندارند.")]
        public string NewPasswordAgain { get; init; }
    }

    /// <summary>
    /// شیء انتقال داده (DTO) برای تغییر تم اپلیکیشن توسط کاربران.
    /// </summary>
    public sealed record ChangeTheme
    {
        [Required(ErrorMessage = "لطفاً یک تم انتخاب کنید.")]
        [EnumDataType(typeof(Theme), ErrorMessage = "تم انتخاب شده نامعتبر است.")]
        public Theme Theme { get; init; }
    }

    /// <summary>
    /// شیء انتقال داده (DTO) برای تغییر رنگ پس‌زمینه صفحه چت توسط کاربران.
    /// </summary>
    public sealed record ChangeChatBackground
    {
        [Required(ErrorMessage = "لطفاً یک رنگ انتخاب کنید.")]
        [RegularExpression(@"^(color[1-9]|#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3}))$", ErrorMessage = "لطفاً یک رنگ معتبر وارد کنید (مثال: color1 یا #FF0000).")]
        public string ChatBackground { get; init; }
    }
}
