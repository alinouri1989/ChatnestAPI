using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Entities.Models;
public sealed class Call
{
    [Key]
    public Guid Id { get; set; }

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

    // Navigation properties
    [ForeignKey("ChatId")]
    public Chat? Chat { get; set; }

    // Add this navigation property
    public ICollection<CallParticipant> CallParticipants { get; set; } = new List<CallParticipant>();
}