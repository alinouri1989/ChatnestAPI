using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    public sealed record PasswordFallbackQuestionRequest
    {
        [Required(ErrorMessage = "لطفاً آدرس ایمیل را وارد کنید.")]
        [EmailAddress(ErrorMessage = "فرمت ایمیل وارد شده صحیح نیست.")]
        public string Email { get; init; }
    }
}
