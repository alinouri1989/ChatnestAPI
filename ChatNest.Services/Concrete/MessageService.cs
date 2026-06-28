using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs;
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

        private async Task<Chat> GetWritableChatAsync(string userId, string chatId, string chatType)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            var chat = await _chatRepository.GetChatByIdAsync(parsedChatId);

            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("گفت و گو یافت نشد یا دسترسی به آن ندارید");

            if (!string.Equals(chat.ChatType, chatType, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Chat type does not match target chat");

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

            return chat;
        }

        private Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>> CreateMessagePayload(
            string chatType,
            string chatId,
            Message message)
        {
            var messageDto = _mapper.Map<MessageDto>(message);

            return new Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>>
            {
                {
                    chatType,
                    new Dictionary<string, Dictionary<string, MessageDto>>
                    {
                        {
                            chatId,
                            new Dictionary<string, MessageDto>
                            {
                                { message.Id.ToString(), messageDto }
                            }
                        }
                    }
                }
            };
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>>, List<string>)> SendMessageAsync(
            string userId, string chatId, string chatType, SendMessage dto)
        {
            var chat = await GetWritableChatAsync(userId, chatId, chatType);
            var parsedChatId = chat.Id;

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

            return (CreateMessagePayload(chatType, chatId, message), chat.ChatParticipants.Select(c => c.UserId).ToList());
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>>, List<string>)> ForwardAttachmentAsync(
            string userId,
            string sourceMessageId,
            string targetChatId,
            string targetChatType)
        {
            var parsedSourceMessageId = ParseRequiredGuid(sourceMessageId, "source message id");
            var sourceMessage = await _messageRepository.GetMessageByIdAsync(parsedSourceMessageId);
            if (sourceMessage == null)
                throw new NotFoundException("پیام مبدا یافت نشد");

            var sourceChat = await _chatRepository.GetChatByIdAsync(sourceMessage.ChatId);
            if (sourceChat == null || !sourceChat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("به پیام مبدا دسترسی ندارید");

            if (sourceMessage.DeletedFor.ContainsKey(userId))
                throw new NotFoundException("پیام مبدا در دسترس نیست");

            if (sourceMessage.Type == MessageContent.Text)
                throw new BadRequestException("Only attachments can be forwarded");

            if (string.IsNullOrWhiteSpace(sourceMessage.Content))
                throw new BadRequestException("Attachment content is unavailable");

            var targetChat = await GetWritableChatAsync(userId, targetChatId, targetChatType);

            var forwardedMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                ChatId = targetChat.Id,
                Content = sourceMessage.Content,
                Type = sourceMessage.Type,
                ThumbnailUrl = sourceMessage.ThumbnailUrl,
                FileName = sourceMessage.FileName,
                FileSize = sourceMessage.FileSize,
                CreatedDate = DateTime.UtcNow,
                Status = new MessageStatus
                {
                    Sent = new Dictionary<string, DateTime> { { userId, DateTime.UtcNow } },
                    Delivered = new Dictionary<string, DateTime>(),
                    Read = new Dictionary<string, DateTime>()
                }
            };

            await _messageRepository.CreateMessageAsync(forwardedMessage);

            return (CreateMessagePayload(targetChatType, targetChatId, forwardedMessage), targetChat.ChatParticipants.Select(c => c.UserId).ToList());
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>>, List<string>)> DeleteMessageAsync(
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

                return (CreateMessagePayload(chatType, chatId, deleteMarker), chat.ChatParticipants.Select(c => c.UserId).ToList());
            }

            // Delete for me only
            else
            {
                var deletedFor = message.DeletedFor;
                deletedFor[userId] = DateTime.UtcNow;
                await _messageRepository.UpdateMessageDeletedForAsync(parsedMessageId, deletedFor);

                var updatedMessage = await _messageRepository.GetMessageByIdAsync(parsedMessageId);
                var result = new Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>>();

                if (updatedMessage != null)
                {
                    result = CreateMessagePayload(chatType, chatId, updatedMessage);
                }

                return (result, new List<string> { userId });
            }
        }

        public async Task<(Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>>, List<string>)> DeliverOrReadMessageAsync(
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

            return (CreateMessagePayload(chatType, chatId, updatedMessage), chat.ChatParticipants.Select(c => c.UserId).ToList());
        }

        public async Task<int> GetTotalMessageCountAsync(Guid chatId)
        {
            return await _messageRepository.GetTotalMessageCountAsync(chatId);
        }

        public async Task<IEnumerable<MessageDto>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 5)
        {
            var messages = await _messageRepository.GetChatMessagesAsync(chatId, skip, take);
            return _mapper.Map<IEnumerable<MessageDto>>(messages);
        }

        public async Task<ChatNest.Shared.DTOs.ChatMessagesPageResponse> GetChatMessagesPageAsync(
            string userId,
            Guid chatId,
            int skip = 0,
            int take = 50)
        {
            var chat = await _chatRepository.GetChatByIdAsync(chatId);
            if (chat == null || !chat.ChatParticipants.Any(u => u.UserId == userId))
                throw new NotFoundException("گفت و گو یافت نشد یا دسترسی به آن ندارید");

            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 100);

            var total = await _messageRepository.GetTotalMessageCountAsync(chatId);
            var messages = await _messageRepository.GetChatMessagesAsync(chatId, skip, take);
            var hasNextPage = skip + take < total;

            return new ChatNest.Shared.DTOs.ChatMessagesPageResponse
            {
                ChatId = chatId.ToString(),
                ChatType = chat.ChatType,
                TotalCount = total,
                Messages = _mapper.Map<IEnumerable<MessageDto>>(messages),
                Skip = skip,
                Take = take,
                PageNumber = (skip / take) + 1,
                PageSize = take,
                HasNextPage = hasNextPage,
                NextSkip = hasNextPage ? skip + take : null,
                HasMore = hasNextPage,
                IsInitial = skip == 0
            };
        }

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
                    ChatType = chat.ChatType,
                    TotalCount = total,
                    Messages = Array.Empty<MessageDto>(),
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
                ChatType = chat.ChatType,
                TotalCount = total,
                Messages = _mapper.Map<IEnumerable<MessageDto>>(messages),
                DayStartUtc = dayStartUtc,
                NextCursorUtc = hasMore ? dayStartUtc : null,
                HasMore = hasMore,
                IsInitial = !beforeUtc.HasValue
            };
        }
    }
}
