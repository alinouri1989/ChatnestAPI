using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete
{
    public sealed class GroupRepository : IGroupRepository
    {
        private readonly ChatNestDbContext _context;

        public GroupRepository(ChatNestDbContext context)
        {
            _context = context;
        }

        public async Task CreateOrUpdateGroupAsync(Group group)
        {
            var existGroup = _context.Groups.Any(c => c.Id == group.Id);
            if (!existGroup)
            {
                if (group.Id == Guid.Empty)
                {
                    group.Id = Guid.NewGuid();
                }

                if (group.CreatedDate == default)
                {
                    group.CreatedDate = DateTime.UtcNow;
                }

                _context.Groups.Add(group);
            }
            else
            {
                _context.Groups.Update(group);
            }

            await _context.SaveWithConcurrencyRetryAsync();
        }

        public async Task<IEnumerable<Group>> GetAllGroupsAsync()
        {
            return await _context.Groups
                .Include(g => g.Creator)
                .ToListAsync();
        }

        public async Task<Group?> GetGroupByIdAsync(Guid groupId)
        {
            return await _context.Groups
                .Include(g => g.Creator)
                .FirstOrDefaultAsync(g => g.Id == groupId);
        }

        public async Task<List<string>> GetGroupParticipantsIdsAsync(Guid groupId)
        {
            var group = await _context.Groups.FindAsync(groupId);
            return group?.Participants?.Where(p => p.Value != GroupParticipant.Former)
                                     .Select(p => p.Key)
                                     .ToList() ?? new List<string>();
        }

        public async Task UpdateGroupParticipantsAsync(Guid groupId, Dictionary<string, GroupParticipant> groupParticipants)
        {
            var group = await _context.Groups.FindAsync(groupId);
            if (group != null)
            {
                group.Participants = groupParticipants;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> DeleteGroupAsync(Guid groupId)
        {
            var group = await _context.Groups.FindAsync(groupId);
            if (group != null)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var linkedCalls = await _context.Calls
                    .Where(c => c.ChatId == groupId)
                    .ToListAsync();
                if (linkedCalls.Count > 0)
                {
                    _context.Calls.RemoveRange(linkedCalls);
                }

                var linkedGroupChat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.Id == groupId && c.ChatType == "Group");
                if (linkedGroupChat != null)
                {
                    _context.Chats.Remove(linkedGroupChat);
                }

                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<Group>> GetUserGroupsAsync(string userId)
        {
            var groups = await _context.Groups
                .Include(g => g.Creator)
                .ToListAsync();

            return groups
                .Where(g => g.Participants.ContainsKey(userId) &&
                            g.Participants[userId] != GroupParticipant.Former)
                .ToList();
        }
    }
}
