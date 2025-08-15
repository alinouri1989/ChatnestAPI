using ChatNest.Entities.Models;

namespace ChatNest.DataAccess.Abstract
{
    public interface ICallRepository
    {
        Task<IEnumerable<Call>> GetCallsAsync();
        Task CreateOrUpdateCallAsync(Call call);
        Task<Call?> GetCallByIdAsync(Guid callId);
        Task<List<string>> GetCallParticipantsByIdAsync(Guid callId);
        Task UpdateCallAsync(Call call);
        Task<bool> DeleteCallAsync(Guid callId);
        Task<IEnumerable<Call>> GetUserCallsAsync(string userId);
    }
}