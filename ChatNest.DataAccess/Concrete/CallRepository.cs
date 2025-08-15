using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete
{

    public sealed class CallRepository : ICallRepository
    {
        private readonly ChatNestDbContext _context;

        public CallRepository(ChatNestDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Call>> GetCallsAsync()
        {
            return await _context.Calls
                .Include(c => c.Chat)
                .ToListAsync();
        }

        public async Task CreateOrUpdateCallAsync(Call call)
        {
            if (call.Id == Guid.Empty)
            {
                call.Id = Guid.NewGuid();
                call.CreatedDate = DateTime.UtcNow;
                _context.Calls.Add(call);
            }
            else
            {
                _context.Calls.Update(call);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<Call?> GetCallByIdAsync(Guid callId)
        {
            return await _context.Calls
                .Include(c => c.Chat)
                .FirstOrDefaultAsync(c => c.Id == callId);
        }

        public async Task<List<string>> GetCallParticipantsByIdAsync(Guid callId)
        {
            var call = await _context.Calls.FindAsync(callId);
            return call?.Participants ?? new List<string>();
        }

        public async Task UpdateCallAsync(Call call)
        {
            _context.Calls.Update(call);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteCallAsync(Guid callId)
        {
            var call = await _context.Calls.FindAsync(callId);
            if (call != null)
            {
                _context.Calls.Remove(call);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<Call>> GetUserCallsAsync(string userId)
        {
            return await _context.Calls
                .Where(c => c.Participants.Contains(userId))
                .Include(c => c.Chat)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
        }
    }
}