namespace ChatNest.Shared.DTOs
{
    public sealed class ChatMessagesPageResponse
    {
        public string ChatId { get; init; } = string.Empty;
        public string ChatType { get; init; } = string.Empty;
        public int TotalCount { get; init; }
        public IEnumerable<MessageDto> Messages { get; init; } = Array.Empty<MessageDto>();
        public int Skip { get; init; }
        public int Take { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; }
        public bool HasNextPage { get; init; }
        public int? NextSkip { get; init; }
        public DateTime? DayStartUtc { get; init; }
        public DateTime? NextCursorUtc { get; init; }
        public bool HasMore { get; init; }
        public bool IsInitial { get; init; }
    }
}
