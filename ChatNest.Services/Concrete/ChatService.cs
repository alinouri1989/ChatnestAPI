using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs;

namespace ChatNest.Services.Concrete
{
    public sealed class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IMapper _mapper;

        public ChatService(
            IChatRepository chatRepository,
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IMessageRepository messageRepository,
            IMapper mapper)
        {
            _chatRepository = chatRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _messageRepository = messageRepository;
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

        public async Task<Dictionary<string, ChatDto>> CreateChatAsync(string userId, string chatType, string recipientId)
        {
            List<string> participants;
            Guid? linkedGroupId = null;

            if (chatType.Equals("Group", StringComparison.OrdinalIgnoreCase))
            {
                if (!Guid.TryParse(recipientId, out var groupId))
                    throw new BadRequestException("Invalid group id");

                linkedGroupId = groupId;

                participants = await _groupRepository.GetGroupParticipantsIdsAsync(groupId);
                if (participants == null || !participants.Any())
                    throw new NotFoundException("Group has no participants");

                if (!participants.Contains(userId))
                    throw new ForbiddenException("Access denied");

                var existingGroupChat = await _chatRepository.GetChatByIdAsync(groupId);
                if (existingGroupChat != null && existingGroupChat.ChatType.Equals("Group", StringComparison.OrdinalIgnoreCase))
                {
                    return new Dictionary<string, ChatDto> { { existingGroupChat.Id.ToString(), _mapper.Map<ChatDto>(existingGroupChat) } };
                }
            }
            else
            {
                participants = new List<string> { userId, recipientId };
            }

            participants = participants
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .ToList();

            foreach (var participantId in participants)
            {
                var user = await _userRepository.GetUserByIdAsync(participantId);
                if (user == null)
                    throw new NotFoundException($"User not found: {participantId}");
            }

            // Check if chat already exists by checking participants (individual chats only)
            if (!linkedGroupId.HasValue)
            {
                var existingChat = await _chatRepository.GetChatByParticipantsAsync(participants);
                if (existingChat != null && existingChat.ChatType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
                {
                    return new Dictionary<string, ChatDto> { { existingChat.Id.ToString(), _mapper.Map<ChatDto>(existingChat) } };
                }
            }

            // Create new chat
            var chat = new Chat
            {
                Id = linkedGroupId ?? Guid.NewGuid(),
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

            return new Dictionary<string, ChatDto> { { chat.Id.ToString(), _mapper.Map<ChatDto>(createdChat) } };
        }
        public async Task<int> GetUserChatsCountAsync(string userId)
        {
            return await _chatRepository.GetUserChatsCountAsync(userId);
        }

        public async Task<(Dictionary<string, Dictionary<string, ChatDto>>, List<string>, List<string>)> GetAllChatsAsync(string userId, int skip = 0, int take = 5)
        {
            try
            {
                var userChats = await _chatRepository.GetUserChatsAsync(userId, skip, take);
                var userGroups = await _groupRepository.GetUserGroupsAsync(userId);
                var result = new Dictionary<string, Dictionary<string, ChatDto>>();
                var individualParticipants = new List<string>();
                var userGroupIds = userGroups.Select(g => g.Id.ToString()).Distinct().ToList();

                // Ensure we always have valid collections
                if (userChats == null || !userChats.Any())
                {
                    return (result, individualParticipants, userGroupIds);
                }

                var orderedChats = userChats
                    .OrderByDescending(c =>
                        c.Messages.Any()
                            ? c.Messages.Max(m => m.CreatedDate)
                            : c.CreatedDate
                    )
                    .ToList();

                var individualChats = orderedChats
                    .Where(c => c.ChatType == "Individual")
                    .ToDictionary(c => c.Id.ToString(), _mapper.Map<ChatDto>);

                var groupChats = orderedChats
                    .Where(c => c.ChatType == "Group")
                    .ToDictionary(c => c.Id.ToString(), _mapper.Map<ChatDto>);


                // Always add the keys, even if empty
                if (individualChats.Any())
                {
                    result.Add("Individual", individualChats);

                    // Participants are already loaded with chats (ChatParticipants include).
                    foreach (var chatEntity in userChats.Where(c => c.ChatType == "Individual"))
                    {
                        var chatParticipants = chatEntity.ChatParticipants
                            .Select(cp => cp.UserId)
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Distinct()
                            .ToList();

                        var otherParticipants = chatParticipants
                            .Where(p => p != userId)
                            .ToList();

                        if (otherParticipants.Count > 0)
                        {
                            individualParticipants.AddRange(otherParticipants);
                        }
                        else if (chatParticipants.Contains(userId))
                        {
                            // Self-chat (Saved Messages) has no "other" participant;
                            // include current user so UI can resolve profile.
                            individualParticipants.Add(userId);
                        }
                    }
                    individualParticipants = individualParticipants.Distinct().ToList();
                }
                else
                {
                    // Add empty Individual section
                    result.Add("Individual", new Dictionary<string, ChatDto>());
                }

                if (groupChats.Any())
                {
                    result.Add("Group", groupChats);
                }
                else
                {
                    // Add empty Group section
                    result.Add("Group", new Dictionary<string, ChatDto>());
                }

                return (result, individualParticipants, userGroupIds);
            }
            catch (Exception ex)
            {
                // Log the exception and return empty but valid structure
                Console.WriteLine($"Error in GetAllChatsAsync: {ex.Message}");

                var emptyResult = new Dictionary<string, Dictionary<string, ChatDto>>
                    {
                        { "Individual", new Dictionary<string, ChatDto>() },
                        { "Group", new Dictionary<string, ChatDto>() }
                    };

                return (emptyResult, new List<string>(), new List<string>());
            }
        }

        public async Task<Dictionary<string, Dictionary<string, ChatDto>>> ClearChatAsync(string userId, string chatType, string chatId)
        {
            var chatGuid = ParseRequiredGuid(chatId, "chat id");
            var chat = await _chatRepository.GetChatByIdAsync(chatGuid);
            if (chat == null)
                throw new NotFoundException("Chat not found");

            // Check if user is participant
            var participants = await _chatRepository.GetChatParticipantsAsync(chatGuid);
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            var clearedAt = DateTime.UtcNow;
            foreach (var message in chat.Messages)
            {
                var deletedFor = message.DeletedFor;
                deletedFor[userId] = clearedAt;
                await _messageRepository.UpdateMessageDeletedForAsync(message.Id, deletedFor);
            }

            var updatedChat = await _chatRepository.GetChatByIdAsync(chatGuid);
            await _chatRepository.DeleteChatAsync(chatGuid);
            var result = new Dictionary<string, Dictionary<string, ChatDto>>
            {
                { chatType, new Dictionary<string, ChatDto> { { chatId, _mapper.Map<ChatDto>(updatedChat!) } } }
            };

            return result;
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> ArchiveIndividualChatAsync(string userId, string chatId)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);
            if (chat == null)
                throw new NotFoundException("Chat not found");

            // Check if user is participant
            var participants = await _chatRepository.GetChatParticipantsAsync(parsedChatId);
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
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);
            if (chat == null)
                throw new NotFoundException("Chat not found");

            // Check if user is participant
            var participants = await _chatRepository.GetChatParticipantsAsync(parsedChatId);
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

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> PinChatAsync(string userId, string chatType, string chatId)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);
            if (chat == null)
                throw new NotFoundException("Chat not found");

            var participants = await _chatRepository.GetChatParticipantsAsync(parsedChatId);
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            var pinnedFor = chat.PinnedFor;
            pinnedFor[userId] = DateTime.UtcNow;
            chat.PinnedFor = pinnedFor;
            await _chatRepository.UpdateChatAsync(chat);

            var normalizedType = chat.ChatType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "Group" : "Individual";
            if (!string.IsNullOrWhiteSpace(chatType))
            {
                normalizedType = chatType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "Group" : "Individual";
            }

            return new Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>
            {
                { normalizedType, new Dictionary<string, Dictionary<string, DateTime>> { { chatId, pinnedFor } } }
            };
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>> UnpinChatAsync(string userId, string chatType, string chatId)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);
            if (chat == null)
                throw new NotFoundException("Chat not found");

            var participants = await _chatRepository.GetChatParticipantsAsync(parsedChatId);
            if (!participants.Contains(userId))
                throw new NotFoundException("Access denied");

            var pinnedFor = chat.PinnedFor;
            pinnedFor.Remove(userId);
            chat.PinnedFor = pinnedFor;
            await _chatRepository.UpdateChatAsync(chat);

            var normalizedType = chat.ChatType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "Group" : "Individual";
            if (!string.IsNullOrWhiteSpace(chatType))
            {
                normalizedType = chatType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "Group" : "Individual";
            }

            return new Dictionary<string, Dictionary<string, Dictionary<string, DateTime>>>
            {
                { normalizedType, new Dictionary<string, Dictionary<string, DateTime>> { { chatId, pinnedFor } } }
            };
        }

        // Add new helper methods
        public async Task AddParticipantToChatAsync(string chatId, string userId)
        {
            await _chatRepository.AddParticipantAsync(ParseRequiredGuid(chatId, "chat id"), userId);
        }

        public async Task RemoveParticipantFromChatAsync(string chatId, string userId)
        {
            await _chatRepository.RemoveParticipantAsync(ParseRequiredGuid(chatId, "chat id"), userId);
        }

        public async Task<List<string>> GetChatParticipantsAsync(string chatId)
        {
            return await _chatRepository.GetChatParticipantsAsync(ParseRequiredGuid(chatId, "chat id"));
        }
    }
}
