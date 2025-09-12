using ChatNest.Entities.Models;

public interface IChatRepository
{
    Task<Chat> AddChatAsync(Chat chat);
    Task<Chat?> GetChatByIdAsync(Guid id);
    Task<List<Chat>> GetChatsByUserIdAsync(string userId);
    Task<IEnumerable<Chat>> GetUserChatsAsync(string userId);
    Task<Chat> UpdateChatAsync(Chat chat);
    Task DeleteChatAsync(Guid id);
    Task AddParticipantAsync(Guid chatId, string userId);
    Task RemoveParticipantAsync(Guid chatId, string userId);
    Task<List<string>> GetChatParticipantsAsync(Guid chatId);

    // Add this missing method
    Task<Chat?> GetChatByParticipantsAsync(List<string> participantIds);
}