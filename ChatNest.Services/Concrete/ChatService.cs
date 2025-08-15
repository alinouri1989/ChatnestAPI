using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;

namespace ChatNest.Services.Concrete
{
    public sealed class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public ChatService(IChatRepository chatRepository, IUserRepository userRepository, IMapper mapper)
        {
            _chatRepository = chatRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<Dictionary<string, Chat>> CreateChatAsync(string userId, string chatType, string recipientId)
        {
            var participants = new List<string> { userId, recipientId };

            // Check if chat already exists
            var existingChat = await _chatRepository.GetChatByParticipantsAsync(participants);
            if (existingChat != null)
            {
                return new Dictionary<string, Chat> { { existingChat.Id.ToString(), existingChat } };
            }

            var chat = new Chat
            {
                ChatType = chatType,
                Participants = participants,
                CreatedDate = DateTime.UtcNow
            };

            await _chatRepository.CreateChatAsync(chat);

            return new Dictionary<string, Chat> { { chat.Id.ToString(), chat } };
        }

        public async Task<(Dictionary<string, Dictionary<string, Chat>>, List<string>, List<string>)> GetAllChatsAsync(string userId)
        {
            var userChats = await _chatRepository.GetUserChatsAsync(userId);
            var result = new Dictionary<string, Dictionary<string, Chat>>();
            var individualParticipants = new List<string>();
            var groupParticipants = new List<string>();

            var individualChats = userChats.Where(c => c.ChatType == "Individual").ToDictionary(c => c.Id.ToString(), c => c);
            var groupChats = userChats.Where(c => c.ChatType == "Group").ToDictionary(c => c.Id.ToString(), c => c);

            if (individualChats.Any())
            {
                result.Add("Individual", individualChats);
                individualParticipants.AddRange(individualChats.Values.SelectMany(c => c.Participants).Distinct().Where(p => p != userId));
            }

            if (groupChats.Any())
            {
                result.Add("Group", groupChats);
                groupParticipants.AddRange(groupChats.Values.SelectMany(c => c.Participants).Distinct().Where(p => p != userId));
            }

            return (result, individualParticipants, groupParticipants);
        }

        public async Task<Dictionary<string, Dictionary<string, Chat>>> ClearChatAsync(string userId, string chatType, string chatId)
        {
            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null || !chat.Participants.Contains(userId))
                throw new NotFoundException("Chat not found or access denied");

            // Clear messages for this user (mark them as deleted)
            // This would require updating the message repository to mark messages as deleted for this user
            // For now, we'll just return the updated chat structure

            var updatedChats = await _chatRepository.GetChatsAsync(chatType);
            var result = new Dictionary<string, Dictionary<string, Chat>>
            {
                { chatType, updatedChats.ToDictionary(c => c.Id.ToString(), c => c) }
            };

            return result;
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> ArchiveIndividualChatAsync(string userId, string chatId)
        {
            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null || !chat.Participants.Contains(userId))
                throw new NotFoundException("Chat not found or access denied");

            var archivedFor = chat.ArchivedFor;
            archivedFor[userId] = DateTime.UtcNow;

            await _chatRepository.UpdateChatArchivedForAsync(Guid.Parse(chatId), archivedFor);

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>
            {
                { "Individual", new Dictionary<string, Dictionary<string, DateTime>> { { chatId, archivedFor } } }
            };

            return result;
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> UnarchiveIndividualChatAsync(string userId, string chatId)
        {
            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null || !chat.Participants.Contains(userId))
                throw new NotFoundException("Chat not found or access denied");

            var archivedFor = chat.ArchivedFor;
            archivedFor.Remove(userId);

            await _chatRepository.UpdateChatArchivedForAsync(Guid.Parse(chatId), archivedFor);

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>
            {
                { "Individual", new Dictionary<string, Dictionary<string, DateTime>> { { chatId, archivedFor } } }
            };

            return result;
        }
    }
}