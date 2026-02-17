using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete;

public class ChatRepository : IChatRepository
{
    private readonly ChatNestDbContext _context;

    public ChatRepository(ChatNestDbContext context)
    {
        _context = context;
    }

    public async Task<Chat> AddChatAsync(Chat chat)
    {
        _context.Chats.Add(chat);
        await _context.SaveChangesAsync();
        return chat;
    }

    public async Task<Chat?> GetChatByIdAsync(Guid id)
    {
        return await _context.Chats
            .Include(c => c.Messages)
            .Include(c => c.ChatParticipants)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Chat>> GetChatsByUserIdAsync(string userId)
    {
        return await _context.Chats
            .Where(c => c.ChatParticipants.Any(cp => cp.UserId == userId))
            .Include(c => c.Messages)
            .Include(c => c.ChatParticipants)
            .ToListAsync();
    }

    public async Task<IEnumerable<Chat>> GetUserChatsAsync(string userId)
    {
        return await _context.Chats
            .Include(c => c.Messages)
            .Include(c => c.ChatParticipants)
            .Where(c => c.ChatParticipants.Any(cp => cp.UserId == userId))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<Chat> UpdateChatAsync(Chat chat)
    {
        _context.Chats.Update(chat);
        await _context.SaveChangesAsync();
        return chat;
    }

    public async Task DeleteChatAsync(Guid id)
    {
        var chat = await _context.Chats.FindAsync(id);
        if (chat != null)
        {
            _context.Chats.Remove(chat);
            await _context.SaveChangesAsync();
        }
    }

    // Add new methods for participant management
    public async Task AddParticipantAsync(Guid chatId, string userId)
    {
        var participant = new ChatParticipant
        {
            ChatId = chatId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };
        try
        {

            _context.ChatParticipants.Add(participant);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {

            throw;
        }
    }

    public async Task RemoveParticipantAsync(Guid chatId, string userId)
    {
        var participant = await _context.ChatParticipants
            .FirstOrDefaultAsync(cp => cp.ChatId == chatId && cp.UserId == userId);

        if (participant != null)
        {
            _context.Set<ChatParticipant>().Remove(participant);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<string>> GetChatParticipantsAsync(Guid chatId)
    {
        return await _context.ChatParticipants
            .Where(cp => cp.ChatId == chatId)
            .Select(cp => cp.UserId)
            .ToListAsync();
    }
    public async Task<Chat?> GetChatByParticipantsAsync(List<string> participantIds)
    {
        if (participantIds == null || !participantIds.Any())
            return null;

        // Sort the participant IDs to ensure consistent comparison
        var sortedParticipants = participantIds.OrderBy(x => x).ToList();

        // Get all chats that have participants matching our list
        var potentialChats = await _context.Chats
            .Include(c => c.ChatParticipants)
            .Include(c => c.Messages)
            .Where(c => c.ChatParticipants.Any(cp => participantIds.Contains(cp.UserId)))
            .ToListAsync();

        // Find chat with exactly matching participants
        var matchingChat = potentialChats.FirstOrDefault(c =>
        {
            var chatParticipantIds = c.ChatParticipants.Select(cp => cp.UserId).OrderBy(x => x).ToList();
            return chatParticipantIds.SequenceEqual(sortedParticipants);
        });

        return matchingChat;
    }
}