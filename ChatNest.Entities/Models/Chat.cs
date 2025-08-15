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
    public string ParticipantsJson { get; set; } = string.Empty;

    [NotMapped]
    public List<string> Participants
    {
        get => string.IsNullOrEmpty(ParticipantsJson) ?
               new List<string>() :
               JsonSerializer.Deserialize<List<string>>(ParticipantsJson) ?? new List<string>();
        set => ParticipantsJson = JsonSerializer.Serialize(value);
    }

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

    // Navigation property
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
