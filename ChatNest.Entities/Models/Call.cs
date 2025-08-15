using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Entities.Models;

public sealed class Call
{
    [Key]
    public Guid Id { get; set; }

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

    public CallType Type { get; set; }
    public CallStatus Status { get; set; }

    public Guid? ChatId { get; set; }

    public TimeSpan? CallDuration { get; set; }

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

    // Navigation property
    [ForeignKey("ChatId")]
    public Chat? Chat { get; set; }
}