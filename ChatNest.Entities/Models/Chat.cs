using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Entities.Models;

public sealed class Chat
{
    [Key]
    public Guid Id { get; set; }

    public string ChatType { get; set; } = "Individual"; // Individual or Group

    // Store as JSON string in database
    public string ArchivedForJson { get; set; } = string.Empty;

    [NotMapped]
    public Dictionary<string, DateTime> ArchivedFor
    {
        get => string.IsNullOrEmpty(ArchivedForJson) ?
               new Dictionary<string, DateTime>() :
               JsonSerializer.Deserialize<Dictionary<string, DateTime>>(ArchivedForJson) ?? new Dictionary<string, DateTime>();
        set => ArchivedForJson = JsonSerializer.Serialize(value);
    }

    public DateTime CreatedDate { get; set; }

    // Navigation properties
    public ICollection<Message> Messages { get; set; } = new List<Message>();

    // Add this navigation property
    public ICollection<ChatParticipant> ChatParticipants { get; set; } = new List<ChatParticipant>();
}