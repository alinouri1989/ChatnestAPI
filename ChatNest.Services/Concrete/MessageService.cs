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
        private readonly IGroupRepository _groupRepository;
        private readonly IMediaStorageRepository _mediaStorageRepository;
        private readonly IMapper _mapper;

        public MessageService(
            IMessageRepository messageRepository,
            IChatRepository chatRepository,
            IGroupRepository groupRepository,
            IMediaStorageRepository mediaStorageRepository,
            IMapper mapper)
        {
            _messageRepository = messageRepository;
            _chatRepository = chatRepository;
            _groupRepository = groupRepository;
            _mediaStorageRepository = mediaStorageRepository;
            _mapper = mapper;
        }

        private static string BuildReplyPreviewContent(Message referencedMessage)
        {
            return referencedMessage.Type switch
            {
                MessageContent.Text => referencedMessage.Content,
                MessageContent.Image => "Photo",
                MessageContent.Video => "Video",
                MessageContent.Audio => "Voice message",
                MessageContent.File => referencedMessage.FileName ?? "File",
                _ => "Message"
            };
        }

        private static Guid ParseRequiredGuid(string value, string parameterName)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            throw new BadRequestException($"Invalid {parameterName}");
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> SendMessageAsync(
            string userId, string chatId, string chatType, SendMessage dto)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("گفت و گو یافت نشد یا دسترسی به آن ندارید");

            if (chatType.Equals("Group", StringComparison.OrdinalIgnoreCase))
            {
                var group = await _groupRepository.GetGroupByIdAsync(chat.Id);
                if (group == null)
                    throw new NotFoundException("Group not found");

                if (group.Kind == GroupKind.Channel)
                {
                    var isAdmin = group.Participants.TryGetValue(userId, out var role) &&
                                  role == GroupParticipant.Admin;
                    if (!isAdmin)
                        throw new ForbiddenException("فقط مدیران کانال دسترسی ارسال پیام دارند");
                }
            }

            Guid? replyToMessageId = null;
            Message? referencedMessage = null;
            if (!string.IsNullOrWhiteSpace(dto.ReplyToMessageId))
            {
                if (!Guid.TryParse(dto.ReplyToMessageId, out var parsedReplyId))
                    throw new BadRequestException("Invalid reply message id");

                referencedMessage = await _messageRepository.GetMessageByIdAsync(parsedReplyId);
                if (referencedMessage == null || referencedMessage.ChatId != chat.Id)
                    throw new NotFoundException("Reply target پیام یافت نشد");

                replyToMessageId = parsedReplyId;
            }

            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                ChatId = parsedChatId,
                Content = dto.Content,
                Type = dto.ContentType,
                ClientMessageId = dto.ClientMessageId,
                ThumbnailUrl = dto.ThumbnailUrl,
                ReplyToMessageId = replyToMessageId,
                ReplyToSenderId = referencedMessage?.SenderId,
                ReplyToType = referencedMessage?.Type,
                ReplyToContent = referencedMessage != null ? BuildReplyPreviewContent(referencedMessage) : null,
                ReplyToFileName = referencedMessage?.FileName,
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
                        message.ThumbnailUrl = thumbUrl?.ToString();

                        break;

                    case MessageContent.Video:
                        (mediaUrl, thumbUrl) = await _mediaStorageRepository.UploadVideoWithThumbnailAsync($"message_{message.Id}", "messages", "message,video", fileStream, dto.FileName);
                        message.ThumbnailUrl = thumbUrl?.ToString();
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
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var parsedMessageId = ParseRequiredGuid(messageId, "message id");
            var message = await _messageRepository.GetMessageByIdAsync(parsedMessageId);
            if (message == null)
                throw new NotFoundException("پیام یافت نشد");

            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("گفت و گو یافت نشد یا دسترسی به آن ندارید");

            if (deletionType == 1) // Delete for everyone (only sender can do this)
            {
                if (message.SenderId != userId)
                    throw new BadRequestException("شما فقط پیام متعلق به خود را می توانید پاک کنید");

                await _messageRepository.DeleteMessageAsync(parsedMessageId);
                var deletedAt = DateTime.UtcNow;
                var deleteMarker = new Message
                {
                    Id = parsedMessageId,
                    ChatId = parsedChatId,
                    SenderId = message.SenderId,
                    Content = string.Empty,
                    Type = MessageContent.Text,
                    CreatedDate = deletedAt,
                    Status = message.Status,
                    DeletedFor = chat.ChatParticipants.ToDictionary(participant => participant.UserId, _ => deletedAt)
                };

                var deletedMessageResult = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>
                {
                    { chatType, new Dictionary<string, Dictionary<string, Message>> { { chatId, new Dictionary<string, Message> { { messageId, deleteMarker } } } } }
                };

                return (deletedMessageResult, chat.ChatParticipants.Select(c => c.UserId).ToList());
            }

            // Delete for me only
            else
            {
                var deletedFor = message.DeletedFor;
                deletedFor[userId] = DateTime.UtcNow;
                await _messageRepository.UpdateMessageDeletedForAsync(parsedMessageId, deletedFor);

                var updatedMessage = await _messageRepository.GetMessageByIdAsync(parsedMessageId);
                var result = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>();

                if (updatedMessage != null)
                {
                    result = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>
                    {
                        { chatType, new Dictionary<string, Dictionary<string, Message>> { { chatId, new Dictionary<string, Message> { { messageId, updatedMessage } } } } }
                    };
                }

                return (result, new List<string> { userId });
            }
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, Message>>>, List<string>)> DeliverOrReadMessageAsync(
            string userId, string chatType, string chatId, string messageId, string fieldName)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var parsedMessageId = ParseRequiredGuid(messageId, "message id");
            var message = await _messageRepository.GetMessageByIdAsync(parsedMessageId);
            if (message == null)
                return default;
            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("گفت و گو یافت نشد یا دسترسی به آن ندارید");

            var statusUpdate = new Dictionary<string, DateTime> { { userId, DateTime.UtcNow } };
            await _messageRepository.UpdateMessageStatusAsync(parsedMessageId, fieldName, statusUpdate);

            var updatedMessage = await _messageRepository.GetMessageByIdAsync(parsedMessageId);
            if (updatedMessage == null)
                throw new NotFoundException("پیام یافت نشد");

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, Message>>>
            {
                { chatType, new Dictionary<string, Dictionary<string, Message>> { { chatId, new Dictionary<string, Message> { { messageId, updatedMessage } } } } }
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
                throw new NotFoundException("گفت و گو یافت نشد یا دسترسی به آن ندارید");

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
