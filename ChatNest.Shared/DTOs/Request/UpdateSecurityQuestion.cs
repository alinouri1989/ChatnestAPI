using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    public sealed record UpdateSecurityQuestion
    {
        [Required(ErrorMessage = "انتخاب سؤال امنیتی الزامی است.")]
        public string QuestionKey { get; init; }

        public string? CustomQuestionText { get; init; }

        [Required(ErrorMessage = "پاسخ سؤال امنیتی الزامی است.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "پاسخ سؤال امنیتی باید بین 2 تا 200 کاراکتر باشد.")]
        public string Answer { get; init; }
    }
}
