using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace ChatNest.API.Models.Requests
{
    public sealed record SendFileMessageFormRequest
    {
        [Required(ErrorMessage = "Chat type is required")]
        public string ChatType { get; init; } = string.Empty;

        [EnumDataType(typeof(MessageContent), ErrorMessage = "Invalid content type")]
        public MessageContent ContentType { get; init; }

        [Required(ErrorMessage = "File payload is required")]
        public IFormFile? File { get; init; }

        public string? ClientMessageId { get; init; }
        public string? ReplyToMessageId { get; init; }
    }
}
