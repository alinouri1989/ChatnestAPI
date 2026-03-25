using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;

namespace ChatNest.Services.Concrete
{
    public sealed class MessageService : IMessageService
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IMediaStorageRepository _mediaStorageRepository;
        private readonly IMapper _mapper;

        public MessageService(
            IMessageRepository messageRepository,
            IChatRepository chatRepository,
            IMediaStorageRepository mediaStorageRepository,
            IMapper mapper)
        {
            _messageRepository = messageRepository;
            _chatRepository = chatRepository;
            _mediaStorageRepository = mediaStorageRepository;
            _mapper = mapper;
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> SendMessageAsync(
            string userId, string chatId, string chatType, SendMessage dto)
        {
            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("Chat not found or access denied");

            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                ChatId = Guid.Parse(chatId),
                Content = dto.Content,
                Type = dto.ContentType,
                ClientMessageId = dto.ClientMessageId,
                CreatedDate = DateTime.UtcNow,
                Status = new MessageStatus
                {
                    Sent = new Dictionary<string, DateTime> { { userId, DateTime.UtcNow } },
                    Delivered = new Dictionary<string, DateTime>(),
                    Read = new Dictionary<string, DateTime>()
                }
            };

            // Current clients send files as Base64 in Content via SignalR.
            // Normalize to dto.File so storage upload logic is reused.
            if (dto.ContentType != MessageContent.Text && dto.File == null && !string.IsNullOrWhiteSpace(dto.Content))
            {
                try
                {
                    dto.File = Convert.FromBase64String(dto.Content);
                }
                catch (FormatException)
                {
                    throw new BadRequestException("Invalid file payload");
                }
            }

            // Handle file uploads
            if (dto.ContentType != MessageContent.Text && dto.File != null)
            {
                using var fileStream = new MemoryStream(dto.File);
                Uri? mediaUrl = null;
                Uri? thumbUrl = null;
                long fileSize = dto.File.Length;

                switch (dto.ContentType)
                {
                    case MessageContent.Image:
                        (mediaUrl, thumbUrl) = await _mediaStorageRepository
                            .UploadPhotoWithThumbnailAsync(
                                $"message_{message.Id}",
                                "messages",
                                "message,image",
                                fileStream,
                                dto.FileName);
                        break;

                    case MessageContent.Video:
                        (mediaUrl, thumbUrl) = await _mediaStorageRepository.UploadVideoWithThumbnailAsync($"message_{message.Id}", "messages", "message,video", fileStream, dto.FileName);
                        break;
                    case MessageContent.Audio:
                        mediaUrl = await _mediaStorageRepository.UploadAudioAsync($"message_{message.Id}", "messages", "message,audio", fileStream, dto.FileName);
                        break;
                    case MessageContent.File:
                        var (url, size) = await _mediaStorageRepository.UploadFileAsync($"message_{message.Id}", "messages", "message,file", fileStream, dto.FileName);
                        mediaUrl = url;
                        fileSize = size;
                        break;
                    default:
                        throw new BadRequestException("Invalid message type");
                }

                message.Content = mediaUrl!.ToString();
                message.ThumbnailUrl = thumbUrl!.ToString();
                message.FileName = dto.FileName;
                message.FileSize = fileSize;
            }
            else if (dto.ContentType != MessageContent.Text)
            {
                throw new BadRequestException("File payload is required for non-text messages");
            }

            await _messageRepository.CreateMessageAsync(message);

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>
            {
                { chatType, new Dictionary<string, Dictionary<string, Message>> { { chatId, new Dictionary<string, Message> { { message.Id.ToString(), message } } } } }
            };

            return (result, chat.ChatParticipants.Select(c => c.UserId).ToList());
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> DeleteMessageAsync(
            string userId, string chatType, string chatId, string messageId, byte deletionType)
        {
            var message = await _messageRepository.GetMessageByIdAsync(Guid.Parse(messageId));
            if (message == null)
                throw new NotFoundException("Message not found");

            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("Chat not found or access denied");

            if (deletionType == 1) // Delete for everyone (only sender can do this)
            {
                if (message.SenderId != userId)
                    throw new BadRequestException("You can only delete your own messages for everyone");

                // Use a tombstone payload instead of physical deletion so clients can update UI
                // consistently (e.g., "This message was deleted") without losing message identity.
                message.Content = "این پیام حذف شده است.";
                message.Type = MessageContent.Text;
                message.FileName = null;
                message.FileSize = null;

                await _messageRepository.UpdateMessageAsync(message);
            }
            else // Delete for me only
            {
                var deletedFor = message.DeletedFor;
                deletedFor[userId] = DateTime.UtcNow;
                await _messageRepository.UpdateMessageDeletedForAsync(Guid.Parse(messageId), deletedFor);
            }

            var updatedMessage = await _messageRepository.GetMessageByIdAsync(Guid.Parse(messageId));
            var result = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>();

            if (updatedMessage != null)
            {
                result = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>
                {
                    { chatType, new Dictionary<string, Dictionary<string, Message>> { { chatId, new Dictionary<string, Message> { { messageId, updatedMessage } } } } }
                };
            }

            return (result, chat.ChatParticipants.Select(c => c.UserId).ToList());
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> DeliverOrReadMessageAsync(
            string userId, string chatType, string chatId, string messageId, string fieldName)
        {
            var message = await _messageRepository.GetMessageByIdAsync(Guid.Parse(messageId));
            if (message == null)
                throw new NotFoundException("Message not found");

            var chat = await _chatRepository.GetChatByIdAsync(Guid.Parse(chatId));
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("Chat not found or access denied");

            var statusUpdate = new Dictionary<string, DateTime> { { userId, DateTime.UtcNow } };
            await _messageRepository.UpdateMessageStatusAsync(Guid.Parse(messageId), fieldName, statusUpdate);

            var updatedMessage = await _messageRepository.GetMessageByIdAsync(Guid.Parse(messageId));
            var result = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>
            {
                { chatType, new Dictionary<string, Dictionary<string, Message>> { { chatId, new Dictionary<string, Message> { { messageId, updatedMessage! } } } } }
            };

            return (result, chat.ChatParticipants.Select(c => c.UserId).ToList());
        }

        public async Task<int> GetTotalMessageCountAsync(Guid chatId)
        {
            return await _messageRepository.GetTotalMessageCountAsync(chatId);
        }

        public async Task<IEnumerable<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 5) =>
                       await _messageRepository.GetChatMessagesAsync(chatId, skip, take);

        public async Task<ChatNest.Shared.DTOs.ChatMessagesPageResponse> GetChatMessagesByDayAsync(
            string userId,
            Guid chatId,
            DateTime? beforeUtc = null)
        {
            var chat = await _chatRepository.GetChatByIdAsync(chatId);
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("Chat not found or access denied");

            var total = await _messageRepository.GetTotalMessageCountAsync(chatId);
            var latest = await _messageRepository.GetLatestMessageDateAsync(chatId, beforeUtc);

            if (!latest.HasValue)
            {
                return new ChatNest.Shared.DTOs.ChatMessagesPageResponse
                {
                    ChatId = chatId.ToString(),
                    TotalCount = total,
                    Messages = Array.Empty<Message>(),
                    DayStartUtc = null,
                    NextCursorUtc = null,
                    HasMore = false,
                    IsInitial = !beforeUtc.HasValue
                };
            }

            var dayStartUtc = DateTime.SpecifyKind(latest.Value.Date, DateTimeKind.Utc);
            var dayEndUtc = dayStartUtc.AddDays(1);

            var messages = await _messageRepository.GetChatMessagesByDateRangeAsync(chatId, dayStartUtc, dayEndUtc);
            var hasMore = await _messageRepository.HasMessagesBeforeAsync(chatId, dayStartUtc);

            return new ChatNest.Shared.DTOs.ChatMessagesPageResponse
            {
                ChatId = chatId.ToString(),
                TotalCount = total,
                Messages = messages,
                DayStartUtc = dayStartUtc,
                NextCursorUtc = hasMore ? dayStartUtc : null,
                HasMore = hasMore,
                IsInitial = !beforeUtc.HasValue
            };
        }
    }
}
