using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    public sealed record ResetPasswordFallback
    {
        [Required(ErrorMessage = "لطفاً آدرس ایمیل خود را وارد کنید.")]
        [EmailAddress(ErrorMessage = "لطفاً یک آدرس ایمیل معتبر وارد کنید.")]
        public string Email { get; init; }

        [Required(ErrorMessage = "شناسه سؤال امنیتی الزامی است.")]
        public string QuestionKey { get; init; }

        [Required(ErrorMessage = "پاسخ سؤال امنیتی الزامی است.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "پاسخ سؤال امنیتی باید بین 2 تا 200 کاراکتر باشد.")]
        public string Answer { get; init; }

        [DataType(DataType.Password)]
        [Required(ErrorMessage = "لطفاً رمز عبور جدید را وارد کنید.")]
        [StringLength(16, MinimumLength = 8, ErrorMessage = "رمز عبور باید حداقل 8 و حداکثر 16 کاراکتر باشد.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "رمز عبور باید حداقل شامل یک حرف بزرگ و یک عدد باشد.")]
        public string NewPassword { get; init; }

        [DataType(DataType.Password)]
        [Required(ErrorMessage = "لطفاً تکرار رمز عبور جدید را وارد کنید.")]
        [Compare("NewPassword", ErrorMessage = "رمزهای عبور مطابقت ندارند.")]
        public string NewPasswordAgain { get; init; }
    }
}
