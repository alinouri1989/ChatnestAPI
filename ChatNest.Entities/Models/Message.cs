using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Entities.Models;

public sealed class Message
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    public long? FileSize { get; set; }

    public MessageContent Type { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;

    public Guid ChatId { get; set; }

    public Guid? ReplyToMessageId { get; set; }

    public string? ReplyToSenderId { get; set; }

    public MessageContent? ReplyToType { get; set; }

    public string? ReplyToContent { get; set; }

    [MaxLength(255)]
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

    // Store as JSON string in database
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

    [NotMapped]
    public string? ClientMessageId { get; set; }

    // Navigation properties
    [ForeignKey("SenderId")]
    public User? Sender { get; set; }

    [ForeignKey("ChatId")]
    public Chat? Chat { get; set; }
}
