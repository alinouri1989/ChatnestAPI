using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
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

        public ChatController(
            IChatService chatService,
            IMessageService messageService,
            IUserService userService,
            IGroupService groupService)
        {
            _chatService = chatService;
            _messageService = messageService;
            _userService = userService;
            _groupService = groupService;
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
