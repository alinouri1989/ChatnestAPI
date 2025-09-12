using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    /// <summary>
    /// شیء انتقال داده (DTO) که امکان ورود کاربران با ایمیل و رمز عبور را فراهم می‌کند.
    /// </summary>
    public sealed record SignInEmail
    {
        [Required(ErrorMessage = "لطفاً آدرس ایمیل خود را وارد کنید.")]
        [EmailAddress(ErrorMessage = "لطفاً یک آدرس ایمیل معتبر وارد کنید.")]
        public string Email { get; init; }


        [Required(ErrorMessage = "لطفاً رمز عبور خود را وارد کنید.")]
        [DataType(DataType.Password)]
        public string Password { get; init; }
    }
}