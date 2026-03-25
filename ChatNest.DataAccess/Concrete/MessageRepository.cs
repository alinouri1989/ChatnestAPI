using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete
{
    public sealed class MessageRepository : IMessageRepository
    {
        private const string DeletedMessageTombstone = "این پیام حذف شده است.";
        private readonly ChatNestDbContext _context;

        public MessageRepository(ChatNestDbContext context)
        {
            _context = context;
        }

        public async Task CreateMessageAsync(Message message)
        {
            if (message.Id == Guid.Empty)
                message.Id = Guid.NewGuid();

            message.CreatedDate = DateTime.UtcNow;

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateMessageDeletedForAsync(Guid messageId, Dictionary<string, DateTime> deletedFor)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                message.DeletedFor = deletedFor;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    _context.Entry(message).State = EntityState.Detached;
                }
            }
        }

        public async Task UpdateMessageStatusAsync(Guid messageId, string fieldName, Dictionary<string, DateTime> fieldData)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                var status = message.Status ?? new MessageStatus();
                status.Delivered ??= new Dictionary<string, DateTime>();
                status.Read ??= new Dictionary<string, DateTime>();

                if (fieldName == "Delivered")
                {
                    foreach (var kvp in fieldData)
                    {
                        status.Delivered[kvp.Key] = kvp.Value;
                    }
                }
                else if (fieldName == "Read")
                {
                    foreach (var kvp in fieldData)
                    {
                        status.Read[kvp.Key] = kvp.Value;
                    }
                }

                message.Status = status;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    _context.Entry(message).State = EntityState.Detached;
                }
            }
        }

        public async Task<Message?> GetMessageByIdAsync(Guid messageId)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.Content != DeletedMessageTombstone);
        }

        public async Task<IEnumerable<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 5)
        {
            return await _context.Messages
                .Where(m => m.ChatId == chatId && m.Content != DeletedMessageTombstone)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<DateTime?> GetLatestMessageDateAsync(Guid chatId, DateTime? beforeUtc = null)
        {
            var query = _context.Messages.Where(m => m.ChatId == chatId && m.Content != DeletedMessageTombstone);
            if (beforeUtc.HasValue)
            {
                query = query.Where(m => m.CreatedDate < beforeUtc.Value);
            }

            return await query
                .OrderByDescending(m => m.CreatedDate)
                .Select(m => (DateTime?)m.CreatedDate)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Message>> GetChatMessagesByDateRangeAsync(Guid chatId, DateTime startUtc, DateTime endUtc)
        {
            return await _context.Messages
                .Where(m => m.ChatId == chatId &&
                            m.CreatedDate >= startUtc &&
                            m.CreatedDate < endUtc &&
                            m.Content != DeletedMessageTombstone)
                .Include(m => m.Sender)
                .OrderBy(m => m.CreatedDate)
                .ToListAsync();
        }

        public async Task<bool> HasMessagesBeforeAsync(Guid chatId, DateTime beforeUtc)
        {
            return await _context.Messages.AnyAsync(m => m.ChatId == chatId &&
                                                         m.CreatedDate < beforeUtc &&
                                                         m.Content != DeletedMessageTombstone);
        }

        public async Task<int> GetTotalMessageCountAsync(Guid chatId)
        {
            return await _context.Messages
                .CountAsync(m => m.ChatId == chatId && m.Content != DeletedMessageTombstone);
        }

        public async Task UpdateMessageAsync(Message message)
        {
            _context.Messages.Update(message);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                _context.Messages.Remove(message);
                try
                {
                    await _context.SaveChangesAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    _context.Entry(message).State = EntityState.Detached;
                    return true;
                }
            }
            return false;
        }

        public async Task<IEnumerable<Message>> SearchMessagesAsync(string searchTerm, Guid? chatId = null)
        {
            var query = _context.Messages.Where(m => m.Content.Contains(searchTerm));

            if (chatId.HasValue)
            {
                query = query.Where(m => m.ChatId == chatId.Value);
            }

            return await query
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedDate)
                .ToListAsync();
        }
    }
}
