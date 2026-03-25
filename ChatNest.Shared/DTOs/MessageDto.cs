using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Shared.DTOs
{
    public class MessageDto
    {
        public Guid Id { get; set; }

        public string Content { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;

        public string? FileName { get; set; }

        public long? FileSize { get; set; }

        public MessageContent Type { get; set; }

        public string SenderId { get; set; } = string.Empty;

        public Guid ChatId { get; set; }

        public Guid? ReplyToMessageId { get; set; }

        public string? ReplyToSenderId { get; set; }

        public MessageContent? ReplyToType { get; set; }

        public string? ReplyToContent { get; set; }

        public string? ReplyToFileName { get; set; }

        // Store as JSON string in database
        public string StatusJson { get; set; } = string.Empty;

        [NotMapped]
        public MessageStatus Status
        {
            get => string.IsNullOrEmpty(StatusJson) ?
                   new MessageStatus() :
                   JsonSerializer.Deserialize<MessageStatus>(StatusJson) ?? new MessageStatus();
            set => StatusJson = JsonSerializer.Serialize(value);
        }

        public string DeletedForJson { get; set; } = string.Empty;

        [NotMapped]
        public Dictionary<string, DateTime> DeletedFor
        {
            get => string.IsNullOrEmpty(DeletedForJson) ?
                   new Dictionary<string, DateTime>() :
                   JsonSerializer.Deserialize<Dictionary<string, DateTime>>(DeletedForJson) ?? new Dictionary<string, DateTime>();
            set => DeletedForJson = JsonSerializer.Serialize(value);
        }

        public DateTime CreatedDate { get; set; }

        public string? ClientMessageId { get; set; }

        public UserDto? Sender { get; set; }

        public ChatDto? Chat { get; set; }
    }
}
