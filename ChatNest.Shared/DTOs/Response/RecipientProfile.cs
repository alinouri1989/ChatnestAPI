using ChatNest.Entities.Models;

namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// شیء انتقال داده (DTO) شامل اطلاعات پروفایل کاربران به‌عنوان گیرنده.
    /// </summary>
    public sealed record RecipientProfile
    {
        public required string DisplayName { get; init; }

        public required string Email { get; init; }

        public required string Biography { get; init; }

        public required string ProfilePhoto { get; init; }

        public DateTime LastConnectionDate { get; set; }
    }
}
