using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request;

public sealed record RequestLoginOtp
{
    [EmailAddress(ErrorMessage = "لطفاً یک آدرس ایمیل معتبر وارد کنید.")]
    public string? Email { get; init; }

    [Phone(ErrorMessage = "شماره موبایل معتبر نیست.")]
    public string? Mobile { get; init; }
}
