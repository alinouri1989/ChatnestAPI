using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request
{
    public sealed record ForwardAttachmentRequest
    {
        [Required]
        public string SourceMessageId { get; init; } = string.Empty;

        [Required]
        public string TargetChatType { get; init; } = string.Empty;
    }
}
