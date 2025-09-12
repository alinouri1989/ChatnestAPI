using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;

public interface ICallService
{
    Task<string> StartCallAsync(string userId, string recipientId, CallType callType);
    Task AcceptCallAsync(string userId, string callId);
    Task<Dictionary<string, Call>> EndCallAsync(string userId, string callId, CallStatus callStatus, DateTime? createdDate);
    Task DeleteCallAsync(string userId, string callId);
    Task<List<string>> GetCallParticipantsAsync(string userId, string callId);
    Task<(Dictionary<string, Dictionary<string, Call>>, List<string>)> GetCallLogsAsync(string userId); // Made async
    Task<Call> GetCallAsync(string userId, string callId);

    // New methods
    Task AddParticipantToCallAsync(string callId, string userId);
    Task RemoveParticipantFromCallAsync(string callId, string userId);
}