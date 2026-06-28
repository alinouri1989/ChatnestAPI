using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    /// <summary>
    /// شیء انتقال داده (DTO) برای ثبت نام کاربران در سیستم.
    /// شامل اطلاعاتی مانند نام، ایمیل، رمز عبور، تاریخ تولد و غیره می‌باشد.
    /// </summary>
    public sealed record SignUp
    {
        [Required(ErrorMessage = "لطفاً نام و نام خانوادگی خود را وارد کنید.")]
        [StringLength(50, MinimumLength = 5, ErrorMessage = "نام و نام خانوادگی شما باید حداقل 5 و حداکثر 50 کاراکتر باشد.")]
        [RegularExpression(@"^(?=.*[\u0600-\u06FFA-Za-z0-9])[\u0600-\u06FFA-Za-z0-9 ]+$", ErrorMessage = "نام نمایشی فقط می‌تواند شامل حروف فارسی/انگلیسی، عدد و فاصله باشد.")]
        public string DisplayName { get; init; }

        [Required(ErrorMessage = "لطفاً آدرس ایمیل خود را وارد کنید.")]
        [EmailAddress(ErrorMessage = "لطفاً یک آدرس ایمیل معتبر وارد کنید.")]
        //[MaxLength(255, ErrorMessage = "آدرس ایمیل شما باید حداکثر 255 کاراکتر باشد.")]
        public string Email { get; init; }

        [DataType(DataType.Password)]
        [Required(ErrorMessage = "لطفاً رمز عبور خود را وارد کنید.")]
        [StringLength(16, MinimumLength = 8, ErrorMessage = "رمز عبور شما باید حداقل 8 و حداکثر 16 کاراکتر باشد.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "رمز عبور شما باید حداقل شامل یک حرف بزرگ و یک عدد باشد.")]
        public string Password { get; init; }

        [DataType(DataType.Password)]
        [Required(ErrorMessage = "لطفاً رمز عبور خود را دوباره وارد کنید.")]
        [Compare("Password", ErrorMessage = "رمزهای عبور مطابقت ندارند.")]
        public string PasswordAgain { get; init; }

        [Required(ErrorMessage = "لطفاً تاریخ تولد خود را وارد کنید.")]
        public DateTime BirthDate { get; init; }

        public string? PhoneNumber { get; set; }
    }
}
