using ChatNest.Entities.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Shared.DTOs
{
    public sealed class GroupDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Uri? Photo { get; set; }

        public string ParticipantsJson { get; set; } = string.Empty;

        [NotMapped]
        public Dictionary<string, GroupParticipant> Participants
        {
            get => string.IsNullOrEmpty(ParticipantsJson) ?
                   new Dictionary<string, GroupParticipant>() :
                   JsonSerializer.Deserialize<Dictionary<string, GroupParticipant>>(ParticipantsJson) ?? new Dictionary<string, GroupParticipant>();
            set => ParticipantsJson = JsonSerializer.Serialize(value);
        }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public UserDto? Creator { get; set; }
    }

}
