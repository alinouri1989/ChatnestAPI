namespace ChatNest.Shared.DTOs
{
    public sealed class ChatSummaryDto
    {
        public Guid Id { get; init; }
        public string ChatType { get; init; } = "Individual";
        public DateTime CreatedDate { get; init; }
        public Dictionary<string, DateTime> ArchivedFor { get; init; } = new();
        public MessageItemDto? LastMessage { get; init; }
        public IReadOnlyCollection<string> ParticipantIds { get; init; } = Array.Empty<string>();
    }
}
