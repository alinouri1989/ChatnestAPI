using ChatNest.API.Hubs;
using ChatNest.API.Models.Requests;
using ChatNest.Entities.Enums;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Utilities;
using ChatNest.Shared.DTOs;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SixLabors.ImageSharp;
using System.Globalization;

namespace ChatNest.API.Controllers
{
    [Route("api/[controller]")]
    public sealed class ChatController : BaseController
    {
        private readonly IChatService _chatService;
        private readonly IMessageService _messageService;
        private readonly IUserService _userService;
        private readonly IGroupService _groupService;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly IUserPresenceTracker _presenceTracker;

        private const long MaxMessageFileBytes = 300L * 1024 * 1024;

        public ChatController(
            IChatService chatService,
            IMessageService messageService,
            IUserService userService,
            IGroupService groupService,
            INotificationService notificationService,
            IHubContext<ChatHub> chatHubContext,
            IUserPresenceTracker presenceTracker)
        {
            _chatService = chatService;
            _messageService = messageService;
            _userService = userService;
            _groupService = groupService;
            _notificationService = notificationService;
            _chatHubContext = chatHubContext;
            _presenceTracker = presenceTracker;
        }

        [HttpGet("Initial")]
        public async Task<IActionResult> Initial([FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 100);

            var (chats, recipientIds, groupIds) = await _chatService.GetAllChatsAsync(UserId, skip, take);
            var recipientProfiles = await _userService.GetRecipientProfilesAsync(recipientIds ?? new List<string>());
            var groupProfiles = await _groupService.GetGroupProfilesAsync(groupIds ?? new List<string>());
            var totalChats = await _chatService.GetUserChatsCountAsync(UserId);

            return Ok(new
            {
                chats = EnsureChatSummaryShape(chats),
                recipientProfiles,
                groupProfiles,
                totalChats,
                skip,
                take,
                hasMore = skip + take < totalChats
            });
        }

        [HttpGet("Total")]
        public async Task<ActionResult<int>> Total()
        {
            return Ok(await _chatService.GetUserChatsCountAsync(UserId));
        }

        [HttpGet("{chatId}/Messages")]
        public async Task<ActionResult<ChatMessagesPageResponse>> Messages(
            string chatId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            return Ok(await _messageService.GetChatMessagesPageAsync(UserId, parsedChatId, skip, take));
        }

        [HttpGet("{chatId}/MessagesByDay")]
        public async Task<ActionResult<ChatMessagesPageResponse>> MessagesByDay(
            string chatId,
            [FromQuery] string? beforeUtc = null)
        {
            var parsedChatId = ParseRequiredGuid(chatId, "chat id");
            DateTime? cursorUtc = null;

            if (!string.IsNullOrWhiteSpace(beforeUtc) &&
                DateTime.TryParse(
                    beforeUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                cursorUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return Ok(await _messageService.GetChatMessagesByDayAsync(UserId, parsedChatId, cursorUtc));
        }

        [HttpPost("{chatId}/Messages")]
        public async Task<IActionResult> SendMessage(
            string chatId,
            [FromQuery] string chatType,
            [FromBody] SendMessage dto)
        {
            if (string.IsNullOrWhiteSpace(chatType))
            {
                throw new BadRequestException("Chat type is required");
            }

            var (message, chatParticipants) = await _messageService.SendMessageAsync(UserId, chatId, chatType, dto);
            await BroadcastMessageAsync(message, chatParticipants);
            await _notificationService.SendNewMessageNotificationAsync(
                UserId,
                chatParticipants,
                chatId,
                chatType,
                CreateNotificationPreview(dto.ContentType, chatId, dto.FileName, dto.Content));

            return Ok(new { accepted = true });
        }

        [HttpDelete("{chatId}/Messages/{messageId}/ForMe")]
        public async Task<IActionResult> DeleteMessageForMe(
            string chatId,
            string messageId,
            [FromQuery] string chatType)
        {
            return await DeleteMessageAsync(chatId, messageId, chatType, deletionType: 0);
        }

        [HttpDelete("{chatId}/Messages/{messageId}/ForEveryone")]
        public async Task<IActionResult> DeleteMessageForEveryone(
            string chatId,
            string messageId,
            [FromQuery] string chatType)
        {
            return await DeleteMessageAsync(chatId, messageId, chatType, deletionType: 1);
        }

        [HttpPost("{chatId}/Messages/ForwardAttachment")]
        public async Task<IActionResult> ForwardAttachment(
            string chatId,
            [FromBody] ForwardAttachmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TargetChatType))
            {
                throw new BadRequestException("Chat type is required");
            }

            var (message, chatParticipants) = await _messageService.ForwardAttachmentAsync(
                UserId,
                request.SourceMessageId,
                chatId,
                request.TargetChatType);

            await BroadcastMessageAsync(message, chatParticipants);
            await _notificationService.SendNewMessageNotificationAsync(
                UserId,
                chatParticipants,
                chatId,
                request.TargetChatType,
                "Forwarded attachment");

            return Ok(message);
        }

        [HttpPost("Messages/{sourceMessageId}/ForwardToUser/{recipientId}")]
        public async Task<IActionResult> ForwardAttachmentToUser(
            string sourceMessageId,
            string recipientId)
        {
            var chat = await _chatService.CreateChatAsync(UserId, "Individual", recipientId);
            var targetChatId = await BroadcastIndividualChatAsync(chat);

            if (string.IsNullOrWhiteSpace(targetChatId))
            {
                throw new BadRequestException("Target chat could not be resolved");
            }

            var (message, chatParticipants) = await _messageService.ForwardAttachmentAsync(
                UserId,
                sourceMessageId,
                targetChatId,
                "Individual");

            await BroadcastMessageAsync(message, chatParticipants);
            await _notificationService.SendNewMessageNotificationAsync(
                UserId,
                chatParticipants,
                targetChatId,
                "Individual",
                "Forwarded attachment");

            return Ok(new
            {
                chatId = targetChatId,
                message
            });
        }

        [HttpPost("{chatId}/Messages/File")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxMessageFileBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxMessageFileBytes)]
        public async Task<IActionResult> SendFileMessage(
            string chatId,
            [FromForm] SendFileMessageFormRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ChatType))
            {
                throw new BadRequestException("Chat type is required");
            }

            if (request.ContentType == MessageContent.Text)
            {
                throw new BadRequestException("Text messages must use the JSON message endpoint");
            }

            if (request.File is null || request.File.Length == 0)
            {
                throw new BadRequestException("File payload is required");
            }

            if (request.File.Length > MaxMessageFileBytes)
            {
                throw new BadRequestException("File is too large");
            }

            await using var stream = request.File.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            ValidateUploadedFileContent(request.ContentType, request.File, fileBytes);

            var dto = new SendMessage
            {
                ContentType = request.ContentType,
                Content = string.Empty,
                File = fileBytes,
                FileName = request.File.FileName,
                ClientMessageId = request.ClientMessageId,
                ReplyToMessageId = request.ReplyToMessageId
            };

            Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>> message;
            List<string> chatParticipants;

            try
            {
                (message, chatParticipants) = await _messageService.SendMessageAsync(UserId, chatId, request.ChatType, dto);
            }
            catch (UnknownImageFormatException)
            {
                throw new BadRequestException("Unsupported image format. Please upload JPEG, PNG, GIF, WebP, BMP, or TIFF.");
            }

            await BroadcastMessageAsync(message, chatParticipants);
            await _notificationService.SendNewMessageNotificationAsync(
                UserId,
                chatParticipants,
                chatId,
                request.ChatType,
                CreateNotificationPreview(request.ContentType, chatId, request.File.FileName));

            return Ok(message);
        }

        private static void ValidateUploadedFileContent(MessageContent contentType, IFormFile file, byte[] bytes)
        {
            if (contentType != MessageContent.Image)
            {
                return;
            }

            if (!IsSupportedRasterImage(bytes))
            {
                throw new BadRequestException("Unsupported image format. Please upload JPEG, PNG, GIF, WebP, BMP, or TIFF.");
            }

            if (string.Equals(file.ContentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(file.FileName), ".svg", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("SVG files must be sent as regular files, not image messages.");
            }
        }

        private static bool IsSupportedRasterImage(byte[] bytes)
        {
            if (bytes.Length >= 3 &&
                bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return true;

            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
                return true;

            if (bytes.Length >= 6 &&
                bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
                bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61)
                return true;

            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return true;

            if (bytes.Length >= 2 &&
                bytes[0] == 0x42 && bytes[1] == 0x4D)
                return true;

            if (bytes.Length >= 4 &&
                ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) ||
                 (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A)))
                return true;

            return false;
        }

        private async Task EnsureChatAccess(string chatId)
        {
            var participants = await _chatService.GetChatParticipantsAsync(chatId);
            if (!participants.Contains(UserId))
            {
                throw new NotFoundException("Chat not found or access denied");
            }
        }

        private static Guid ParseRequiredGuid(string value, string parameterName)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            throw new BadRequestException($"Invalid {parameterName}");
        }

        private async Task BroadcastMessageAsync(
            Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>> message,
            IEnumerable<string> chatParticipants)
        {
            foreach (var participant in chatParticipants)
            {
                await _chatHubContext.Clients.User(participant).SendAsync("ReceiveGetMessages", message);
            }
        }

        private async Task<IActionResult> DeleteMessageAsync(
            string chatId,
            string messageId,
            string chatType,
            byte deletionType)
        {
            if (string.IsNullOrWhiteSpace(chatType))
            {
                throw new BadRequestException("Chat type is required");
            }

            var (message, chatParticipants) = await _messageService.DeleteMessageAsync(
                UserId,
                chatType,
                chatId,
                messageId,
                deletionType);

            await BroadcastMessageAsync(message, chatParticipants);
            return Ok(message);
        }

        private async Task<string?> BroadcastIndividualChatAsync(Dictionary<string, ChatDto> chat)
        {
            var chatEntity = chat.Values.FirstOrDefault();
            if (chatEntity == null)
            {
                return null;
            }

            var chatId = chatEntity.Id.ToString();
            var chatParticipants = await _chatService.GetChatParticipantsAsync(chatId);
            var chatResponse = new Dictionary<string, Dictionary<string, ChatDto>> { { "Individual", chat } };

            foreach (var participant in chatParticipants)
            {
                await _chatHubContext.Clients.User(participant).SendAsync("ReceiveCreateChat", chatResponse);
            }

            var recipientProfiles = await _userService.GetRecipientProfilesAsync(chatParticipants);
            ApplyPresenceState(recipientProfiles);

            foreach (var participant in chatParticipants)
            {
                var profileUserId = chatParticipants.FirstOrDefault(p => p != participant) ?? participant;
                if (!recipientProfiles.TryGetValue(profileUserId, out var profileData))
                {
                    continue;
                }

                await _chatHubContext.Clients.User(participant).SendAsync(
                    "ReceiveRecipientProfiles",
                    new Dictionary<string, object>
                    {
                        { profileUserId, profileData }
                    });
            }

            return chatId;
        }

        private void ApplyPresenceState(Dictionary<string, RecipientProfile> profiles)
        {
            foreach (var (userId, profile) in profiles)
            {
                profile.IsOnline = _presenceTracker.IsOnline(userId);
            }
        }

        private static string CreateNotificationPreview(
            MessageContent contentType,
            string chatId,
            string? fileName = null,
            string? content = null)
        {
            return contentType switch
            {
                MessageContent.Text => string.IsNullOrWhiteSpace(content)
                    ? "پیام جدید"
                    : CryptoJsAesDecryptor.DecryptOrOriginal(content, chatId),
                MessageContent.Image => "Photo",
                MessageContent.Video => "Video",
                MessageContent.Audio => "Voice message",
                MessageContent.File => string.IsNullOrWhiteSpace(fileName) ? "File" : fileName,
                _ => "پیام جدید"
            };
        }

        private static MessageItemDto ToMessageItemDto(MessageDto message)
        {
            return new MessageItemDto
            {
                Id = message.Id,
                Content = message.Content,
                ThumbnailUrl = message.ThumbnailUrl,
                FileName = message.FileName,
                FileSize = message.FileSize,
                Type = message.Type,
                SenderId = message.SenderId,
                SenderDisplayName = message.Sender?.DisplayName ?? string.Empty,
                SenderProfilePhoto = message.Sender?.ProfilePhoto?.ToString(),
                SenderUserIdentifier = message.Sender?.UserIdentifier,
                ChatId = message.ChatId,
                ReplyToMessageId = message.ReplyToMessageId,
                ReplyToSenderId = message.ReplyToSenderId,
                ReplyToType = message.ReplyToType,
                ReplyToContent = message.ReplyToContent,
                ReplyToFileName = message.ReplyToFileName,
                Status = message.Status,
                CreatedDate = message.CreatedDate,
                ClientMessageId = message.ClientMessageId
            };
        }

        private static ChatSummaryDto ToChatSummaryDto(ChatDto chat)
        {
            var lastMessage = chat.Messages
                .OrderByDescending(message => message.CreatedDate)
                .FirstOrDefault();

            return new ChatSummaryDto
            {
                Id = chat.Id,
                ChatType = chat.ChatType,
                CreatedDate = chat.CreatedDate,
                ArchivedFor = chat.ArchivedFor,
                LastMessage = lastMessage == null ? null : ToMessageItemDto(lastMessage),
                ParticipantIds = chat.ChatParticipants
                    .Select(participant => participant.UserId)
                    .Where(userId => !string.IsNullOrWhiteSpace(userId))
                    .Distinct()
                    .ToList()
            };
        }

        private static Dictionary<string, Dictionary<string, ChatSummaryDto>> EnsureChatSummaryShape(
            Dictionary<string, Dictionary<string, ChatDto>>? chats)
        {
            chats ??= new Dictionary<string, Dictionary<string, ChatDto>>();

            var summaries = chats.ToDictionary(
                group => group.Key,
                group => group.Value.ToDictionary(
                    chat => chat.Key,
                    chat => ToChatSummaryDto(chat.Value)));

            summaries.TryAdd("Individual", new Dictionary<string, ChatSummaryDto>());
            summaries.TryAdd("Group", new Dictionary<string, ChatSummaryDto>());
            return summaries;
        }
    }
}
