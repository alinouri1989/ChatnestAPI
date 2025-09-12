using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
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

            // Check if chat already exists by checking participants
            var existingChat = await _chatRepository.GetChatByParticipantsAsync(participants);
            if (existingChat != null)
            {
                return new Dictionary<string, Chat> { { existingChat.Id.ToString(), existingChat } };
            }

            // Create new chat
            var chat = new Chat
            {
                Id = Guid.NewGuid(),
                ChatType = chatType,
                CreatedDate = DateTime.UtcNow
            };

            // Create the chat first
            await _chatRepository.AddChatAsync(chat);

            // Add participants to the chat
            foreach (var participantId in participants)
            {
                await _chatRepository.AddParticipantAsync(chat.Id, participantId);
            }

            // Reload chat with participants
            var createdChat = await _chatRepository.GetChatByIdAsync(chat.Id);

            return new Dictionary<string, Chat> { { chat.Id.ToString(), createdChat } };
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
                // Get participants from junction table
                foreach (var chat in individualChats.Values)
                {
                    var participants = await _chatRepository.GetChatParticipantsAsync(chat.Id);
                    individualParticipants.AddRange(participants.Where(p => p != userId));
                }
                individualParticipants = individualParticipants.Distinct().ToList();
            }

            if (groupChats.Any())
            {
                result.Add("Group", groupChats);
                // Get participants from junction table
                foreach (var chat in groupChats.Values)
                {
                    var participants = await _chatRepository.GetChatParticipantsAsync(chat.Id);
                    groupParticipants.AddRange(participants.Where(p => p != userId));
                }
                groupParticipants = groupParticipants.Distinct().ToList();
            }

            return (result, individualParticipants, groupParticipants);
        }

        public async Task<Dictionary<string, Dictionary<string, Chat>>> ClearChatAsync(string userId, string chatType, string chatId)
        {
            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null)
                throw new NotFoundException("Chat not found");

            // Check if user is participant
            var participants = await _chatRepository.GetChatParticipantsAsync(Guid.Parse(chatId));
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            // Clear messages for this user (mark them as deleted)
            // This would require updating the message repository to mark messages as deleted for this user
            // For now, we'll just return the updated chat structure

            var updatedChats = await _chatRepository.GetChatsByUserIdAsync(userId);
            var filteredChats = updatedChats.Where(c => c.ChatType == chatType);
            var result = new Dictionary<string, Dictionary<string, Chat>>
            {
                { chatType, filteredChats.ToDictionary(c => c.Id.ToString(), c => c) }
            };

            return result;
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> ArchiveIndividualChatAsync(string userId, string chatId)
        {
            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null)
                throw new NotFoundException("Chat not found");

            // Check if user is participant
            var participants = await _chatRepository.GetChatParticipantsAsync(Guid.Parse(chatId));
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            var archivedFor = chat.ArchivedFor;
            archivedFor[userId] = DateTime.UtcNow;
            chat.ArchivedFor = archivedFor;

            await _chatRepository.UpdateChatAsync(chat);

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>
            {
                { "Individual", new Dictionary<string, Dictionary<string, DateTime>> { { chatId, archivedFor } } }
            };

            return result;
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> UnarchiveIndividualChatAsync(string userId, string chatId)
        {
            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null)
                throw new NotFoundException("Chat not found");

            // Check if user is participant
            var participants = await _chatRepository.GetChatParticipantsAsync(Guid.Parse(chatId));
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            var archivedFor = chat.ArchivedFor;
            archivedFor.Remove(userId);
            chat.ArchivedFor = archivedFor;

            await _chatRepository.UpdateChatAsync(chat);

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>
            {
                { "Individual", new Dictionary<string, Dictionary<string, DateTime>> { { chatId, archivedFor } } }
            };

            return result;
        }

        // Add new helper methods
        public async Task AddParticipantToChatAsync(string chatId, string userId)
        {
            await _chatRepository.AddParticipantAsync(Guid.Parse(chatId), userId);
        }

        public async Task RemoveParticipantFromChatAsync(string chatId, string userId)
        {
            await _chatRepository.RemoveParticipantAsync(Guid.Parse(chatId), userId);
        }

        public async Task<List<string>> GetChatParticipantsAsync(string chatId)
        {
            return await _chatRepository.GetChatParticipantsAsync(Guid.Parse(chatId));
        }
    }
}