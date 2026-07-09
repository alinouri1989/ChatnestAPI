using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request;

public sealed record VerifyLoginOtp
{
    [EmailAddress(ErrorMessage = "لطفاً یک آدرس ایمیل معتبر وارد کنید.")]
    public string? Email { get; init; }

    [Phone(ErrorMessage = "شماره موبایل معتبر نیست.")]
    public string? Mobile { get; init; }

    [Required(ErrorMessage = "کد تأیید الزامی است.")]
    [StringLength(8, MinimumLength = 4, ErrorMessage = "کد تأیید معتبر نیست.")]
    public string Code { get; init; } = string.Empty;
}
