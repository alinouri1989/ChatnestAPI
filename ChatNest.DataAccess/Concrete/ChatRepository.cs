using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChatNest.DataAccess.Concrete;

public class ChatRepository : IChatRepository
{
    private const string DeletedMessageTombstone = "این پیام حذف شده است.";
    private readonly ChatNestDbContext _context;
    private readonly ILogger<ChatRepository> _logger;

    public ChatRepository(ChatNestDbContext context, ILogger<ChatRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    private IQueryable<Chat> ApplyGroupIntegrityFilter(IQueryable<Chat> query)
    {
        return query.Where(c =>
            c.ChatType != "Group" ||
            _context.Groups.Any(g => g.Id == c.Id)
        );
    }

    public async Task<Chat> AddChatAsync(Chat chat)
    {
        try
        {
            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();
            return chat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddChatAsync");
            throw;
        }
    }

    public async Task<Chat?> GetChatByIdAsync(Guid id)
    {
        try
        {
            return await ApplyGroupIntegrityFilter(_context.Chats)
                .Include(c => c.Messages.Where(m => m.Content != null && m.Content != DeletedMessageTombstone))
                .Include(c => c.ChatParticipants)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChatByIdAsync");
            throw;
        }
    }

    public async Task<List<Chat>> GetChatsByUserIdAsync(string userId, int skip = 0, int take = 5)
    {
        try
        {
            return await ApplyGroupIntegrityFilter(_context.Chats)
                .Where(c => c.ChatParticipants.Any(cp => cp.UserId == userId))
                .Include(c => c.Messages.Where(m => m.Content != null && m.Content != DeletedMessageTombstone))
                .Include(c => c.ChatParticipants)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChatsByUserIdAsync");
            throw;
        }
    }

    public async Task<IEnumerable<Chat>> GetUserChatsAsync(string userId, int skip = 0, int take = 5)
    {
        try
        {
            var query = ApplyGroupIntegrityFilter(_context.Chats)
                .Where(c => c.ChatParticipants.Any(cp => cp.UserId == userId))
                .OrderByDescending(c =>
                    c.Messages
                        .Where(m => m.Content != null && m.Content != DeletedMessageTombstone)
                        .Select(m => (DateTime?)m.CreatedDate)
                        .Max() ?? c.CreatedDate)
                .Skip(skip)
                .Take(take);

            return await query
                .Include(c => c.Messages
                    .Where(m => m.Content != null && m.Content != DeletedMessageTombstone)
                    .OrderByDescending(m => m.CreatedDate)
                    .Take(10))
                .Include(c => c.ChatParticipants)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserChatsAsync");
            throw;
        }
    }

    public async Task<int> GetUserChatsCountAsync(string userId)
    {
        try
        {
            return await ApplyGroupIntegrityFilter(_context.Chats)
                .Include(c => c.ChatParticipants)
                .CountAsync(c => c.ChatParticipants.Any(cp => cp.UserId == userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserChatsCountAsync");
            throw;
        }
    }

    public async Task<Chat> UpdateChatAsync(Chat chat)
    {
        try
        {
            _context.Chats.Update(chat);
            await _context.SaveChangesAsync();
            return chat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateChatAsync");
            throw;
        }
    }

    public async Task DeleteChatAsync(Guid id)
    {
        try
        {
            var chat = await _context.Chats.FindAsync(id);
            if (chat != null)
            {
                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteChatAsync");
            throw;
        }
    }

    public async Task AddParticipantAsync(Guid chatId, string userId)
    {
        try
        {
            var participant = new ChatParticipant
            {
                ChatId = chatId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };

            _context.ChatParticipants.Add(participant);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddParticipantAsync");
            throw;
        }
    }

    public async Task RemoveParticipantAsync(Guid chatId, string userId)
    {
        try
        {
            var participant = await _context.ChatParticipants
                .FirstOrDefaultAsync(cp => cp.ChatId == chatId && cp.UserId == userId);

            if (participant != null)
            {
                _context.ChatParticipants.Remove(participant);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RemoveParticipantAsync");
            throw;
        }
    }

    public async Task<List<string>> GetChatParticipantsAsync(Guid chatId)
    {
        try
        {
            return await _context.ChatParticipants
                .Where(cp => cp.ChatId == chatId)
                .Select(cp => cp.UserId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChatParticipantsAsync");
            throw;
        }
    }

    public async Task<Chat?> GetChatByParticipantsAsync(List<string> participantIds)
    {
        try
        {
            if (participantIds == null || participantIds.Count == 0)
                return null;

            var sorted = participantIds.OrderBy(x => x).ToList();

            var potentialChats = await _context.Chats
                .Include(c => c.ChatParticipants)
                .Include(c => c.Messages.Where(m => m.Content != null && m.Content != DeletedMessageTombstone))
                .Where(c => c.ChatParticipants.Any(cp => participantIds.Contains(cp.UserId)))
                .ToListAsync();

            foreach (var chat in potentialChats)
            {
                if (chat.ChatParticipants == null)
                    continue;

                var chatIds = chat.ChatParticipants
                    .Select(cp => cp.UserId)
                    .OrderBy(x => x)
                    .ToList();

                if (sorted.SequenceEqual(chatIds))
                    return chat;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChatByParticipantsAsync");
            throw;
        }
    }
}
