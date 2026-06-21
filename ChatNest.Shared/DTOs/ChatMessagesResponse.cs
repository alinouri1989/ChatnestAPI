namespace ChatNest.Shared.DTOs
{
    public record ChatMessagesResponse(int TotalCount, IEnumerable<MessageDto> Messages);
}
