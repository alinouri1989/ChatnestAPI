using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Entities.Models;

public sealed class Group
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Uri? Photo { get; set; }

    // Store as JSON string in database
    public string ParticipantsJson { get; set; } = string.Empty;

    [NotMapped]
    public Dictionary<string, GroupParticipant> Participants
    {
        get => string.IsNullOrEmpty(ParticipantsJson) ?
               new Dictionary<string, GroupParticipant>() :
               JsonSerializer.Deserialize<Dictionary<string, GroupParticipant>>(ParticipantsJson) ?? new Dictionary<string, GroupParticipant>();
        set => ParticipantsJson = JsonSerializer.Serialize(value);
    }

    [Required]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    // Navigation property
    [ForeignKey("CreatedBy")]
    public User? Creator { get; set; }
}
