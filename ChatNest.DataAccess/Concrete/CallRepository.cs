using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete;

public class CallRepository : ICallRepository
{
    private readonly ChatNestDbContext _context;

    public CallRepository(ChatNestDbContext context)
    {
        _context = context;
    }

    public async Task<Call> AddCallAsync(Call call)
    {
        _context.Calls.Add(call);
        await _context.SaveChangesAsync();
        return call;
    }

    public async Task<Call?> GetCallByIdAsync(Guid id)
    {
        return await _context.Calls
            .Include(c => c.Chat)
            .Include(c => c.CallParticipants)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Call>> GetCallsByUserIdAsync(string userId)
    {
        return await _context.Calls
            .Where(c => c.CallParticipants.Any(cp => cp.UserId == userId))
            .Include(c => c.Chat)
            .Include(c => c.CallParticipants)
            .ToListAsync();
    }

    public async Task<IEnumerable<Call>> GetUserCallsAsync(string userId)
    {
        return await _context.Calls
            .Include(c => c.Chat)
            .Include(c => c.CallParticipants)
            .Where(c => c.CallParticipants.Any(cp => cp.UserId == userId))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<Call> UpdateCallAsync(Call call)
    {
        _context.Calls.Update(call);
        await _context.SaveChangesAsync();
        return call;
    }

    public async Task DeleteCallAsync(Guid id)
    {
        var call = await _context.Calls.FindAsync(id);
        if (call != null)
        {
            _context.Calls.Remove(call);
            await _context.SaveChangesAsync();
        }
    }

    // Add new methods for participant management
    public async Task AddParticipantAsync(Guid callId, string userId)
    {
        var participant = new CallParticipant
        {
            CallId = callId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };

        _context.Set<CallParticipant>().Add(participant);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveParticipantAsync(Guid callId, string userId)
    {
        var participant = await _context.Set<CallParticipant>()
            .FirstOrDefaultAsync(cp => cp.CallId == callId && cp.UserId == userId);

        if (participant != null)
        {
            _context.Set<CallParticipant>().Remove(participant);
            await _context.SaveChangesAsync();
        }
    }
}