using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;

namespace ChatNest.DataAccess.Abstract
{
    public interface IGroupRepository
    {
        Task CreateOrUpdateGroupAsync(Group group);
        Task<IEnumerable<Group>> GetAllGroupsAsync();
        Task<Group?> GetGroupByIdAsync(Guid groupId);
        Task<Dictionary<Guid, Group>> GetGroupsByIdsAsync(IEnumerable<Guid> groupIds);
        Task<List<string>> GetGroupParticipantsIdsAsync(Guid groupId);
        Task UpdateGroupParticipantsAsync(Guid groupId, Dictionary<string, GroupParticipant> groupParticipants);
        Task<bool> DeleteGroupAsync(Guid groupId);
        Task<IEnumerable<Group>> GetUserGroupsAsync(string userId);
    }
}
