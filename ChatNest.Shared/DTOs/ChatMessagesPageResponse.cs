using ChatNest.Entities.Models;

namespace ChatNest.Shared.DTOs
{
    public sealed class ChatMessagesPageResponse
    {
        public string ChatId { get; init; } = string.Empty;
        public int TotalCount { get; init; }
        public IEnumerable<Message> Messages { get; init; } = Array.Empty<Message>();
        public DateTime? DayStartUtc { get; init; }
        public DateTime? NextCursorUtc { get; init; }
        public bool HasMore { get; init; }
        public bool IsInitial { get; init; }
    }
}
