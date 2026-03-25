using AutoMapper;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using ChatNest.Services.Exceptions;

namespace ChatNest.Services.Concrete
{
    public sealed class CallService : ICallService
    {
        private readonly ICallRepository _callRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IMapper _mapper;

        public CallService(ICallRepository callRepository, IChatRepository chatRepository, IMapper mapper)
        {
            _callRepository = callRepository;
            _chatRepository = chatRepository;
            _mapper = mapper;
        }

        private static Guid ParseRequiredGuid(string value, string parameterName)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            throw new BadRequestException($"Invalid {parameterName}");
        }

        public async Task<string> StartCallAsync(string userId, string recipientId, CallType callType)
        {
            var participants = new List<string> { userId, recipientId };

            // Create the call first
            var call = new Call
            {
                Id = Guid.NewGuid(),
                Type = callType,
                Status = CallStatus.Pending,
                CreatedDate = DateTime.UtcNow
            };

            // Try to find an existing chat between participants
            var chat = await _chatRepository.GetChatByParticipantsAsync(participants);
            if (chat != null)
            {
                call.ChatId = chat.Id;
            }

            // Create the call
            await _callRepository.AddCallAsync(call);

            // Add participants to the call
            foreach (var participantId in participants)
            {
                await _callRepository.AddParticipantAsync(call.Id, participantId);
            }

            return call.Id.ToString();
        }

        public async Task AcceptCallAsync(string userId, string callId)
        {
            var parsedCallId = ParseRequiredGuid(callId, "call id");
            var call = await _callRepository.GetCallByIdAsync(parsedCallId);
            if (call == null)
                throw new NotFoundException("Call not found");

            // Check if user is participant
            var participants = await GetCallParticipantsAsync(userId, callId);
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            if (call.Status == CallStatus.Accepted || call.Status == CallStatus.Ongoing)
                return;

            if (call.Status != CallStatus.Pending)
                throw new BadRequestException("Call is not in pending state");

            call.Status = CallStatus.Accepted;
            await _callRepository.UpdateCallAsync(call);
        }

        public async Task<Dictionary<string, Call>> EndCallAsync(string userId, string callId, CallStatus callStatus, DateTime? createdDate)
        {
            var parsedCallId = ParseRequiredGuid(callId, "call id");
            var call = await _callRepository.GetCallByIdAsync(parsedCallId);
            if (call == null)
                throw new NotFoundException("Call not found");

            // Check if user is participant
            var participants = await GetCallParticipantsAsync(userId, callId);
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            call.Status = callStatus;

            if (createdDate.HasValue)
            {
                call.CallDuration = DateTime.UtcNow - createdDate.Value;
            }

            await _callRepository.UpdateCallAsync(call);

            var result = new Dictionary<string, Call>
            {
                { call.Id.ToString(), call }
            };

            return result;
        }

        public async Task DeleteCallAsync(string userId, string callId)
        {
            var parsedCallId = ParseRequiredGuid(callId, "call id");
            var call = await _callRepository.GetCallByIdAsync(parsedCallId);
            if (call == null)
                throw new NotFoundException("Call not found");

            // Check if user is participant
            var participants = await GetCallParticipantsAsync(userId, callId);
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            var deletedFor = call.DeletedFor;
            deletedFor[userId] = DateTime.UtcNow;
            call.DeletedFor = deletedFor;

            await _callRepository.UpdateCallAsync(call);
        }

        public async Task<List<string>> GetCallParticipantsAsync(string userId, string callId)
        {
            var call = await _callRepository.GetCallByIdAsync(ParseRequiredGuid(callId, "call id"));
            if (call == null)
                throw new NotFoundException("Call not found");

            // Get participants from junction table
            var participants = call.CallParticipants.Select(cp => cp.UserId).ToList();

            // Check if requesting user is a participant
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            return participants;
        }

        public async Task<(Dictionary<string, Dictionary<string, Call>>, List<string>)> GetCallLogsAsync(string userId)
        {
            var userCalls = await _callRepository.GetUserCallsAsync(userId);

            // Filter out calls that are deleted for this user
            var visibleCalls = userCalls.Where(c => !c.DeletedFor.ContainsKey(userId)).ToList();

            var callDict = visibleCalls.ToDictionary(c => c.Id.ToString(), c => c);
            var result = new Dictionary<string, Dictionary<string, Call>>
            {
                { "calls", callDict }
            };

            // Get all participants from the visible calls
            var allParticipants = new List<string>();
            foreach (var call in visibleCalls)
            {
                var participants = call.CallParticipants.Select(cp => cp.UserId).ToList();
                allParticipants.AddRange(participants);
            }

            var uniqueParticipants = allParticipants.Distinct().Where(p => p != userId).ToList();

            return (result, uniqueParticipants);
        }

        public async Task<Call> GetCallAsync(string userId, string callId)
        {
            var call = await _callRepository.GetCallByIdAsync(ParseRequiredGuid(callId, "call id"));
            if (call == null)
                throw new NotFoundException("Call not found");

            // Check if user is participant
            var participants = call.CallParticipants.Select(cp => cp.UserId).ToList();
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            return call;
        }

        // Add new helper methods
        public async Task AddParticipantToCallAsync(string callId, string userId)
        {
            await _callRepository.AddParticipantAsync(ParseRequiredGuid(callId, "call id"), userId);
        }

        public async Task RemoveParticipantFromCallAsync(string callId, string userId)
        {
            await _callRepository.RemoveParticipantAsync(ParseRequiredGuid(callId, "call id"), userId);
        }
    }
}
