using ChatNest.API.Hubs;
using ChatNest.API.Models.Requests;
using ChatNest.Entities.Enums;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Utilities;
using ChatNest.Shared.DTOs;
using ChatNest.Shared.DTOs.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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

        private const long MaxMessageFileBytes = 200L * 1024 * 1024;

        public ChatController(
            IChatService chatService,
            IMessageService messageService,
            IUserService userService,
            IGroupService groupService,
            INotificationService notificationService,
            IHubContext<ChatHub> chatHubContext)
        {
            _chatService = chatService;
            _messageService = messageService;
            _userService = userService;
            _groupService = groupService;
            _notificationService = notificationService;
            _chatHubContext = chatHubContext;
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
                chats = EnsureChatShape(chats),
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

            return Ok(message);
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

            var dto = new SendMessage
            {
                ContentType = request.ContentType,
                Content = string.Empty,
                File = memoryStream.ToArray(),
                FileName = request.File.FileName,
                ClientMessageId = request.ClientMessageId,
                ReplyToMessageId = request.ReplyToMessageId
            };

            var (message, chatParticipants) = await _messageService.SendMessageAsync(UserId, chatId, request.ChatType, dto);
            await BroadcastMessageAsync(message, chatParticipants);
            await _notificationService.SendNewMessageNotificationAsync(
                UserId,
                chatParticipants,
                chatId,
                request.ChatType,
                CreateNotificationPreview(request.ContentType, chatId, request.File.FileName));

            return Ok(message);
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

        private static Dictionary<string, Dictionary<string, ChatDto>> EnsureChatShape(
            Dictionary<string, Dictionary<string, ChatDto>>? chats)
        {
            chats ??= new Dictionary<string, Dictionary<string, ChatDto>>();
            chats.TryAdd("Individual", new Dictionary<string, ChatDto>());
            chats.TryAdd("Group", new Dictionary<string, ChatDto>());
            return chats;
        }
    }
}
