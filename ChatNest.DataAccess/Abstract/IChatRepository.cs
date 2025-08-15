using ChatNest.Entities.Models;

namespace ChatNest.DataAccess.Abstract
{
    public interface IChatRepository
    {
        Task<IEnumerable<Chat>> GetChatsAsync(string chatType);
        Task CreateChatAsync(Chat chat);
        Task<Chat?> GetChatByIdAsync(Guid chatId);
        Task<List<string>> GetChatParticipantsByIdAsync(Guid chatId);
        Task UpdateChatArchivedForAsync(Guid chatId, Dictionary<string, DateTime> archivedFor);
        Task UpdateChatAsync(Chat chat);
        Task<bool> DeleteChatAsync(Guid chatId);
        Task<Chat?> GetChatByParticipantsAsync(List<string> participants);
        Task<IEnumerable<Chat>> GetUserChatsAsync(string userId);
    }
}