using ChatNest.Entities.Models;

namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// شیء انتقال داده (DTO) شامل اطلاعات پروفایل، اطلاعات تماس و تنظیمات حساب کاربر.
    /// </summary>
    public sealed record UserInfo
    {
        public required string DisplayName { get; init; }

        public required string Email { get; init; }

        public string? PhoneNumber { get; init; }

        public required string Biography { get; init; }

        public required string ProviderId { get; init; }

        public required Uri ProfilePhoto { get; init; }

        public required UserSettings UserSettings { get; set; }
    }
}
