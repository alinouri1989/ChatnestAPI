using ChatNest.Shared.DTOs;

public interface IChatService
{
    Task<Dictionary<string, ChatDto>> CreateChatAsync(string userId, string chatType, string recipientId);
    Task<(Dictionary<string, Dictionary<string, ChatDto>>, List<string>, List<string>)> GetAllChatsAsync(string userId, int skip = 0, int take = 5); // Made async
    Task<int> GetUserChatsCountAsync(string userId);
    Task<Dictionary<string, Dictionary<string, ChatDto>>> ClearChatAsync(string userId, string chatType, string chatId);
    Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> ArchiveIndividualChatAsync(string userId, string chatId);
    Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> UnarchiveIndividualChatAsync(string userId, string chatId);

    // New methods
    Task AddParticipantToChatAsync(string chatId, string userId);
    Task RemoveParticipantFromChatAsync(string chatId, string userId);
    Task<List<string>> GetChatParticipantsAsync(string chatId);
}