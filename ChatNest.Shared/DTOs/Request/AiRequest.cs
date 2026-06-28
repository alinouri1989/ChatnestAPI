using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    /// <summary>
    /// شیء انتقال داده (DTO) که نمایانگر درخواست ارسالی به مدل هوش مصنوعی است.
    /// </summary>
    public sealed record AiRequest
    {
        [Required(ErrorMessage = "مدل هوش مصنوعی باید انتخاب شود.")]
        public string AiModel { get; init; }


        [Required(ErrorMessage = "لطفاً یک پرامپت وارد کنید.")]
        [StringLength(1000, MinimumLength = 2, ErrorMessage = "پرامپت باید حداقل 2 و حداکثر 1000 کاراکتر باشد.")]
        public string Prompt { get; init; }
        public string? Model { get; set; }
    }
}