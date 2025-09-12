using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    /// <summary>
    /// شیء انتقال داده (DTO) برای ارسال پیام توسط کاربران.
    /// شامل محتوای پیام و نوع محتوا می‌باشد.
    /// </summary>
    public sealed record SendMessage
    {
        [Required(ErrorMessage = "لطفاً یک نوع محتوا انتخاب کنید.")]
        [EnumDataType(typeof(MessageContent), ErrorMessage = "محتوای انتخاب شده نامعتبر است.")]
        public MessageContent ContentType { get; init; }

        [Required(ErrorMessage = "لطفاً یک پیام وارد کنید.")]
        public string Content { get; init; }
        public byte[]? File { get; set; }
        public string? FileName { get; set; }
    }
}