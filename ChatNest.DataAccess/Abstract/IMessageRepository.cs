using ChatNest.Entities.Models;

namespace ChatNest.DataAccess.Abstract
{
    public interface IMessageRepository
    {
        Task CreateMessageAsync(Message message);
        Task UpdateMessageDeletedForAsync(Guid messageId, Dictionary<string, DateTime> deletedFor);
        Task UpdateMessageStatusAsync(Guid messageId, string fieldName, Dictionary<string, DateTime> fieldData);
        Task<Message?> GetMessageByIdAsync(Guid messageId);
        Task<IEnumerable<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 5);
        Task<DateTime?> GetLatestMessageDateAsync(Guid chatId, DateTime? beforeUtc = null);
        Task<IEnumerable<Message>> GetChatMessagesByDateRangeAsync(Guid chatId, DateTime startUtc, DateTime endUtc);
        Task<bool> HasMessagesBeforeAsync(Guid chatId, DateTime beforeUtc);
        Task UpdateMessageAsync(Message message);
        Task<int> GetTotalMessageCountAsync(Guid chatId);
        Task<bool> DeleteMessageAsync(Guid messageId);
        Task<IEnumerable<Message>> SearchMessagesAsync(string searchTerm, Guid? chatId = null);
    }
}
