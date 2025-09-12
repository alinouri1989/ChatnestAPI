using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    /// <summary>
    /// شیء انتقال داده (DTO) برای ایجاد گروه جدید.
    /// شامل نام گروه، توضیحات، اطلاعات عکس و شرکت‌کنندگان می‌باشد.
    /// </summary>
    public sealed record CreateGroup
    {
        [Required(ErrorMessage = "لطفاً نام گروه را وارد کنید.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "نام گروه باید حداقل 2 و حداکثر 50 کاراکتر باشد.")]
        public string Name { get; init; }


        [MaxLength(100, ErrorMessage = "توضیحات گروه باید حداکثر 100 کاراکتر باشد.")]
        public string? Description { get; init; }


        public string? Photo { get; init; }


        public string? PhotoUrl { get; init; }


        [Required(ErrorMessage = "برای ایجاد گروه باید حداقل یک عضو اضافه شود.")]
        public string Participants { get; init; }
        public List<string>? SelectedParticipants { get; set; }
    }
}