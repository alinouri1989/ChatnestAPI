using ChatNest.Entities.Models;

namespace ChatNest.Shared.DTOs
{
    public record ChatMessagesResponse(int TotalCount, IEnumerable<Message> Messages);
}
