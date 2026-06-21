using ChatNest.Entities.Models;

public interface ICallRepository
{
    Task<Call> AddCallAsync(Call call);
    Task<Call?> GetCallByIdAsync(Guid id);
    Task<List<Call>> GetCallsByUserIdAsync(string userId);
    Task<IEnumerable<Call>> GetUserCallsAsync(string userId);
    Task<IEnumerable<Call>> GetUserCallsAsync(string userId, int skip, int take);
    Task<int> GetUserCallsCountAsync(string userId);
    Task<Call> UpdateCallAsync(Call call);
    Task DeleteCallAsync(Guid id);

    // Add these new methods
    Task AddParticipantAsync(Guid callId, string userId);
    Task RemoveParticipantAsync(Guid callId, string userId);
}