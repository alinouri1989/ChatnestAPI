using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;

namespace ChatNest.Shared.DTOs
{
    public sealed class MessageItemDto
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
        public string ThumbnailUrl { get; init; } = string.Empty;
        public string? FileName { get; init; }
        public long? FileSize { get; init; }
        public MessageContent Type { get; init; }
        public string SenderId { get; init; } = string.Empty;
        public string SenderDisplayName { get; init; } = string.Empty;
        public string? SenderProfilePhoto { get; init; }
        public string? SenderUserIdentifier { get; init; }
        public Guid ChatId { get; init; }
        public Guid? ReplyToMessageId { get; init; }
        public string? ReplyToSenderId { get; init; }
        public MessageContent? ReplyToType { get; init; }
        public string? ReplyToContent { get; init; }
        public string? ReplyToFileName { get; init; }
        public MessageStatus Status { get; init; } = new();
        public DateTime CreatedDate { get; init; }
        public string? ClientMessageId { get; init; }
    }
}
