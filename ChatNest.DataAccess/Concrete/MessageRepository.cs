using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete
{
    public sealed class MessageRepository : IMessageRepository
    {
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
                await _context.SaveChangesAsync();
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
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Message?> GetMessageByIdAsync(Guid messageId)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task<IEnumerable<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 5)
        {
            return await _context.Messages
                .Where(m => m.ChatId == chatId)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
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
                await _context.SaveChangesAsync();
                return true;
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
