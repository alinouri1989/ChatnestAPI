using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Shared.DTOs
{
    public sealed class ChatDto
    {
        public Guid Id { get; set; }

        public string ChatType { get; set; } = "Individual"; // Individual or Group

        public string ArchivedForJson { get; set; } = string.Empty;
        public string PinnedForJson { get; set; } = string.Empty;

        [NotMapped]
        public Dictionary<string, DateTime> ArchivedFor
        {
            get => string.IsNullOrEmpty(ArchivedForJson) ?
                   new Dictionary<string, DateTime>() :
                   JsonSerializer.Deserialize<Dictionary<string, DateTime>>(ArchivedForJson) ?? new Dictionary<string, DateTime>();
            set => ArchivedForJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public Dictionary<string, DateTime> PinnedFor
        {
            get => string.IsNullOrEmpty(PinnedForJson) ?
                   new Dictionary<string, DateTime>() :
                   JsonSerializer.Deserialize<Dictionary<string, DateTime>>(PinnedForJson) ?? new Dictionary<string, DateTime>();
            set => PinnedForJson = JsonSerializer.Serialize(value);
        }

        public DateTime CreatedDate { get; set; }

        public ICollection<MessageDto> Messages { get; set; } = new List<MessageDto>();

        public ICollection<ChatParticipantDto> ChatParticipants { get; set; } = new List<ChatParticipantDto>();
    }
}
