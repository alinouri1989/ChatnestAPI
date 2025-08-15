using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
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

        public async Task<string> StartCallAsync(string userId, string recipientId, CallType callType)
        {
            var participants = new List<string> { userId, recipientId };

            var call = new Call
            {
                Participants = participants,
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

            await _callRepository.CreateOrUpdateCallAsync(call);

            return call.Id.ToString();
        }

        public async Task AcceptCallAsync(string userId, string callId)
        {
            var call = await _callRepository.GetCallByIdAsync(Guid.Parse(callId));
            if (call == null || !call.Participants.Contains(userId))
                throw new NotFoundException("Call not found or access denied");

            if (call.Status != CallStatus.Pending)
                throw new BadRequestException("Call is not in pending state");

            call.Status = CallStatus.Accepted;
            await _callRepository.UpdateCallAsync(call);
        }

        public async Task<Dictionary<string, Call>> EndCallAsync(string userId, string callId, CallStatus callStatus, DateTime? createdDate)
        {
            var call = await _callRepository.GetCallByIdAsync(Guid.Parse(callId));
            if (call == null || !call.Participants.Contains(userId))
                throw new NotFoundException("Call not found or access denied");

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
            var call = await _callRepository.GetCallByIdAsync(Guid.Parse(callId));
            if (call == null || !call.Participants.Contains(userId))
                throw new NotFoundException("Call not found or access denied");

            var deletedFor = call.DeletedFor;
            deletedFor[userId] = DateTime.UtcNow;

            call.DeletedFor = deletedFor;
            await _callRepository.UpdateCallAsync(call);
        }

        public async Task<List<string>> GetCallParticipantsAsync(string userId, string callId)
        {
            var call = await _callRepository.GetCallByIdAsync(Guid.Parse(callId));
            if (call == null || !call.Participants.Contains(userId))
                throw new NotFoundException("Call not found or access denied");

            return await _callRepository.GetCallParticipantsByIdAsync(Guid.Parse(callId));
        }

        public async Task<(Dictionary<string, Dictionary<string, Call>>, List<string>)> GetCallLogs(string userId)
        {
            var userCalls = await _callRepository.GetUserCallsAsync(userId);

            // Filter out calls that are deleted for this user
            var visibleCalls = userCalls.Where(c => !c.DeletedFor.ContainsKey(userId)).ToList();

            var callDict = visibleCalls.ToDictionary(c => c.Id.ToString(), c => c);
            var result = new Dictionary<string, Dictionary<string, Call>>
            {
                { "calls", callDict }
            };

            var participants = visibleCalls.SelectMany(c => c.Participants).Distinct().Where(p => p != userId).ToList();

            return (result, participants);
        }

        public async Task<Call> GetCallAsync(string userId, string callId)
        {
            var call = await _callRepository.GetCallByIdAsync(Guid.Parse(callId));
            if (call == null || !call.Participants.Contains(userId))
                throw new NotFoundException("Call not found or access denied");

            return call;
        }
    }
}