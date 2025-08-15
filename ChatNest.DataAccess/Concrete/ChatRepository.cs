using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete
{
    public sealed class ChatRepository : IChatRepository
    {
        private readonly ChatNestDbContext _context;

        public ChatRepository(ChatNestDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Chat>> GetChatsAsync(string chatType)
        {
            return await _context.Chats
                .Where(c => c.ChatType == chatType)
                .Include(c => c.Messages)
                .ToListAsync();
        }

        public async Task CreateChatAsync(Chat chat)
        {
            chat.Id = Guid.NewGuid();
            chat.CreatedDate = DateTime.UtcNow;

            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();
        }

        public async Task<Chat?> GetChatByIdAsync(Guid chatId)
        {
            return await _context.Chats
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == chatId);
        }

        public async Task<List<string>> GetChatParticipantsByIdAsync(Guid chatId)
        {
            var chat = await _context.Chats.FindAsync(chatId);
            return chat?.Participants ?? new List<string>();
        }

        public async Task UpdateChatArchivedForAsync(Guid chatId, Dictionary<string, DateTime> archivedFor)
        {
            var chat = await _context.Chats.FindAsync(chatId);
            if (chat != null)
            {
                chat.ArchivedFor = archivedFor;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateChatAsync(Chat chat)
        {
            _context.Chats.Update(chat);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteChatAsync(Guid chatId)
        {
            var chat = await _context.Chats.FindAsync(chatId);
            if (chat != null)
            {
                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<Chat?> GetChatByParticipantsAsync(List<string> participants)
        {
            return await _context.Chats
                .FirstOrDefaultAsync(c => c.Participants.Count == participants.Count &&
                                         participants.All(p => c.Participants.Contains(p)));
        }

        public async Task<IEnumerable<Chat>> GetUserChatsAsync(string userId)
        {
            return await _context.Chats
                .Where(c => c.Participants.Contains(userId))
                .Include(c => c.Messages)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
        }
    }
}