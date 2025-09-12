using ChatNest.Entities.Models;

public interface IChatService
{
    Task<Dictionary<string, Chat>> CreateChatAsync(string userId, string chatType, string recipientId);
    Task<(Dictionary<string, Dictionary<string, Chat>>, List<string>, List<string>)> GetAllChatsAsync(string userId); // Made async
    Task<Dictionary<string, Dictionary<string, Chat>>> ClearChatAsync(string userId, string chatType, string chatId);
    Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> ArchiveIndividualChatAsync(string userId, string chatId);
    Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> UnarchiveIndividualChatAsync(string userId, string chatId);

    // New methods
    Task AddParticipantToChatAsync(string chatId, string userId);
    Task RemoveParticipantFromChatAsync(string chatId, string userId);
    Task<List<string>> GetChatParticipantsAsync(string chatId);
}