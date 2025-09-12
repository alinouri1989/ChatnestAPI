using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatNest.API.Hubs
{
    /// <summary>
    /// کلاس هاب SignalR برای مدیریت عملیات گفتگو در زمان واقعی.
    /// اتصالات کاربر، شروع گفتگو، ارسال پیام، فهرست‌بندی گفتگوها و عملیات گروه را مدیریت می‌کند.
    /// </summary>
    [Authorize]
    public sealed class ChatHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IGroupService _groupService;
        private readonly IChatService _chatService;
        private readonly IUserService _userService;

        /// <summary>
        /// شناسه کاربر فعلی (UserId) را برمی‌گرداند.
        /// شناسه کاربر از مقدار <see cref="ClaimTypes.NameIdentifier"/> در JWT گرفته می‌شود.
        /// </summary>
        /// <returns>شناسه منحصربه‌فرد کاربر فعلی.</returns>
        /// <exception cref="UnauthorizedAccessException">
        /// در صورتی که شناسه کاربر یافت نشود یا با مقدار null مواجه شود پرتاب می‌شود.
        /// </exception>
        private string UserId
        {
            get
            {
                var identity = Context?.User?.Identity as ClaimsIdentity;
                var userId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                    throw new UnauthorizedAccessException("User ID not found in token");

                return userId;
            }
        }

        /// <summary>
        /// یک نمونه جدید از کلاس <see cref="ChatHub"/> را ایجاد می‌کند.
        /// </summary>
        /// <param name="messageService">وابستگی <see cref="IMessageService"/> برای عملیات پیام.</param>
        /// <param name="groupService">وابستگی <see cref="IGroupService"/> برای عملیات گروه.</param>
        /// <param name="chatService">وابستگی <see cref="IChatService"/> برای عملیات گفتگو.</param>
        /// <param name="userService">وابستگی <see cref="IUserService"/> برای عملیات کاربر.</param>
        public ChatHub(IMessageService messageService, IGroupService groupService, IChatService chatService, IUserService userService)
        {
            _messageService = messageService;
            _groupService = groupService;
            _chatService = chatService;
            _userService = userService;
        }

        /// <summary>
        /// متدی که زمان اتصال کاربر به هاب فراخوانی می‌شود.
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public override async Task OnConnectedAsync()
        {
            try
            {
                await _userService.UpdateLastConnectionDateAsync(UserId, DateTime.UtcNow);
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ConnectionError", new { message = "خطای اتصال رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// متدی که زمان قطع اتصال کاربر از هاب فراخوانی می‌شود.
        /// </summary>
        /// <param name="exception">خطای رخ داده در طول اتصال (در صورت وجود).</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                await _userService.UpdateLastConnectionDateAsync(UserId, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                // Log the exception but don't send to client since they're disconnecting
                Console.WriteLine($"Error updating last connection date: {ex.Message}");
            }
            finally
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        /// <summary>
        /// تمام گفتگوهای کاربر، پروفایل‌های گیرنده و پروفایل‌های گروه را بارگذاری کرده و به کلاینت ارسال می‌کند.
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> که عملیات ناهمزمان را نمایندگی می‌کند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task Initial()
        {
            try
            {
                var (chats, chatsRecipientIds, userGroupIds) = await _chatService.GetAllChatsAsync(UserId);

                // Ensure we always send valid data
                var safeChats = chats ?? CreateEmptyChatsStructure();
                var safeRecipientIds = chatsRecipientIds ?? new List<string>();
                var safeGroupIds = userGroupIds ?? new List<string>();

                // Get profiles
                var recipientProfiles = await GetRecipientProfilesSafely(safeRecipientIds);
                var groupProfiles = await GetGroupProfilesSafely(safeGroupIds);

                // Send the data
                await Clients.Caller.SendAsync("ReceiveInitialChats", safeChats);
                await Clients.Caller.SendAsync("ReceiveInitialGroupProfiles", groupProfiles);
                await Clients.Caller.SendAsync("ReceiveInitialRecipientChatProfiles", recipientProfiles);
            }
            catch (Exception ex)
            {
                await SendEmptyDataToClient();
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        private Dictionary<string, Dictionary<string, Chat>> CreateEmptyChatsStructure()
        {
            return new Dictionary<string, Dictionary<string, Chat>>
                {
                    { "Individual", new Dictionary<string, Chat>() },
                    { "Group", new Dictionary<string, Chat>() }
                };
        }

        private async Task<Dictionary<string, ChatNest.Shared.DTOs.Response.RecipientProfile>> GetRecipientProfilesSafely(List<string> recipientIds)
        {
            try
            {
                return recipientIds.Any()
                    ? await _userService.GetRecipientProfilesAsync(recipientIds)
                    : new Dictionary<string, ChatNest.Shared.DTOs.Response.RecipientProfile>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting recipient profiles: {ex.Message}");
                return new Dictionary<string, ChatNest.Shared.DTOs.Response.RecipientProfile>();
            }
        }

        private async Task<Dictionary<string, ChatNest.Shared.DTOs.Response.GroupProfile>> GetGroupProfilesSafely(List<string> groupIds)
        {
            try
            {
                return groupIds.Any()
                    ? await _groupService.GetGroupProfilesAsync(groupIds)
                    : new Dictionary<string, ChatNest.Shared.DTOs.Response.GroupProfile>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting group profiles: {ex.Message}");
                return new Dictionary<string, ChatNest.Shared.DTOs.Response.GroupProfile>();
            }
        }

        private async Task SendEmptyDataToClient()
        {
            var emptyChats = CreateEmptyChatsStructure();
            var emptyRecipientProfiles = new Dictionary<string, ChatNest.Shared.DTOs.Response.RecipientProfile>();
            var emptyGroupProfiles = new Dictionary<string, ChatNest.Shared.DTOs.Response.GroupProfile>();

            await Clients.Caller.SendAsync("ReceiveInitialChats", emptyChats);
            await Clients.Caller.SendAsync("ReceiveInitialGroupProfiles", emptyGroupProfiles);
            await Clients.Caller.SendAsync("ReceiveInitialRecipientChatProfiles", emptyRecipientProfiles);
        }

        /// <summary>
        /// یک گفتگوی جدید شروع می‌کند و اطلاعات گفتگو را به شرکت‌کنندگان ارسال می‌کند.
        /// </summary>
        /// <param name="chatType">نوع گفتگو ("Individual" یا "Group").</param>
        /// <param name="recipientId">شناسه گیرنده‌ای که در گفتگو شرکت خواهد کرد.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task CreateChat(string chatType, string recipientId)
        {
            try
            {
                var chat = await _chatService.CreateChatAsync(UserId, chatType, recipientId);

                if (chatType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
                {
                    var chatEntity = chat.Values.FirstOrDefault();
                    if (chatEntity != null)
                    {
                        var chatParticipants = await _chatService.GetChatParticipantsAsync(chatEntity.Id.ToString());

                        // Send chat to all participants
                        var chatResponse = new Dictionary<string, Dictionary<string, Chat>> { { "Individual", chat } };
                        foreach (var participant in chatParticipants)
                        {
                            await Clients.User(participant).SendAsync("ReceiveCreateChat", chatResponse);
                        }

                        // Send recipient profiles to participants
                        var recipientProfiles = await _userService.GetRecipientProfilesAsync(chatParticipants);

                        foreach (var participant in chatParticipants)
                        {
                            var otherParticipantId = chatParticipants.FirstOrDefault(p => p != participant);
                            if (!string.IsNullOrEmpty(otherParticipantId) && recipientProfiles.ContainsKey(otherParticipantId))
                            {
                                var profileResponse = new Dictionary<string, object>
                                {
                                    { otherParticipantId, recipientProfiles[otherParticipantId] }
                                };
                                await Clients.User(participant).SendAsync("ReceiveRecipientProfiles", profileResponse);
                            }
                        }
                    }
                }
                else if (chatType.Equals("Group", StringComparison.OrdinalIgnoreCase))
                {
                    // For group chats, recipientId would be the groupId
                    var groupParticipants = await _groupService.GetGroupParticipantsAsync(UserId, recipientId);

                    var chatResponse = new Dictionary<string, Dictionary<string, Chat>> { { "Group", chat } };
                    foreach (var participant in groupParticipants)
                    {
                        await Clients.User(participant).SendAsync("ReceiveCreateChat", chatResponse);
                    }
                }
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// محتوای یک گفتگوی مشخص را پاک می‌کند.
        /// </summary>
        /// <param name="chatType">نوع گفتگو ("Individual" یا "Group").</param>
        /// <param name="chatId">شناسه گفتگویی که باید پاک شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task ClearChat(string chatType, string chatId)
        {
            try
            {
                var chat = await _chatService.ClearChatAsync(UserId, chatType, chatId);
                await Clients.User(UserId).SendAsync("ReceiveClearChat", chat);
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// یک گفتگوی مشخص را آرشیو می‌کند.
        /// </summary>
        /// <param name="chatId">شناسه گفتگویی که باید آرشیو شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task ArchiveChat(string chatId)
        {
            try
            {
                var archivedFor = await _chatService.ArchiveIndividualChatAsync(UserId, chatId);
                await Clients.User(UserId).SendAsync("ReceiveArchiveChat", archivedFor);
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// یک گفتگوی مشخص را از آرشیو خارج می‌کند.
        /// </summary>
        /// <param name="chatId">شناسه گفتگویی که باید از آرشیو خارج شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task UnarchiveChat(string chatId)
        {
            try
            {
                var archivedFor = await _chatService.UnarchiveIndividualChatAsync(UserId, chatId);
                await Clients.User(UserId).SendAsync("ReceiveUnarchiveChat", archivedFor);
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// پیام دریافتی از کاربر را به شرکت‌کنندگان گفتگوی مشخص شده ارسال می‌کند.
        /// </summary>
        /// <param name="chatType">نوع گفتگو ("Individual" یا "Group").</param>
        /// <param name="chatId">شناسه گفتگویی که پیام به آن ارسال می‌شود.</param>
        /// <param name="dto">DTO نمایندگی کننده پیام ارسالی.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو یا پیام پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task SendMessage(string chatType, string chatId, SendMessage dto)
        {
            try
            {
                var (message, chatParticipants) = await _messageService.SendMessageAsync(UserId, chatId, chatType, dto);

                // Send message to all participants
                foreach (var participant in chatParticipants)
                {
                    await Clients.User(participant).SendAsync("ReceiveGetMessages", message);
                }
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// تحویل یک پیام را علامت‌گذاری می‌کند و به شرکت‌کنندگان گفتگو اطلاع می‌دهد.
        /// </summary>
        /// <param name="chatType">نوع گفتگو ("Individual" یا "Group").</param>
        /// <param name="chatId">شناسه گفتگویی که پیام در آن تحویل داده می‌شود.</param>
        /// <param name="messageId">شناسه پیامی که باید تحویل داده شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو یا پیام پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task DeliverMessage(string chatType, string chatId, string messageId)
        {
            try
            {
                var (message, chatParticipants) = await _messageService.DeliverOrReadMessageAsync(UserId, chatType, chatId, messageId, "Delivered");

                // Send updated message status to all participants
                foreach (var participant in chatParticipants)
                {
                    await Clients.User(participant).SendAsync("ReceiveGetMessages", message);
                }
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// خوانده شدن یک پیام را علامت‌گذاری می‌کند و به شرکت‌کنندگان گفتگو اطلاع می‌دهد.
        /// </summary>
        /// <param name="chatType">نوع گفتگو ("Individual" یا "Group").</param>
        /// <param name="chatId">شناسه گفتگویی که پیام در آن خوانده می‌شود.</param>
        /// <param name="messageId">شناسه پیامی که باید خوانده شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو یا پیام پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task ReadMessage(string chatType, string chatId, string messageId)
        {
            try
            {
                var (message, chatParticipants) = await _messageService.DeliverOrReadMessageAsync(UserId, chatType, chatId, messageId, "Read");

                // Send updated message status to all participants
                foreach (var participant in chatParticipants)
                {
                    await Clients.User(participant).SendAsync("ReceiveGetMessages", message);
                }
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// پیام مشخص شده را حذف می‌کند و به شرکت‌کنندگان گفتگو اطلاع می‌دهد.
        /// </summary>
        /// <param name="chatType">نوع گفتگو ("Individual" یا "Group").</param>
        /// <param name="chatId">شناسه گفتگویی که پیام مورد حذف در آن قرار دارد.</param>
        /// <param name="messageId">شناسه پیامی که باید حذف شود.</param>
        /// <param name="deletionType">نوع حذف را مشخص می‌کند ("0" یا "1").</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن داده‌های گفتگو یا پیام پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task DeleteMessage(string chatType, string chatId, string messageId, byte deletionType)
        {
            try
            {
                var (message, chatParticipants) = await _messageService.DeleteMessageAsync(UserId, chatType, chatId, messageId, deletionType);

                // Send updated message to all participants (or empty message if deleted for everyone)
                foreach (var participant in chatParticipants)
                {
                    await Clients.User(participant).SendAsync("ReceiveGetMessages", message);
                }
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// کاربر را در گروه مشخص شده عضو می‌کند.
        /// </summary>
        /// <param name="groupId">شناسه گروهی که باید در آن عضو شد.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        public async Task JoinGroup(string groupId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Group_{groupId}");
                await Clients.Caller.SendAsync("JoinedGroup", new { groupId });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "هنگام عضویت در گروه خطا رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// کاربر را از گروه مشخص شده خارج می‌کند.
        /// </summary>
        /// <param name="groupId">شناسه گروهی که باید از آن خارج شد.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        public async Task LeaveGroup(string groupId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Group_{groupId}");
                await Clients.Caller.SendAsync("LeftGroup", new { groupId });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "هنگام خروج از گروه خطا رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// وضعیت آنلاین کاربر را به‌روزرسانی می‌کند.
        /// </summary>
        /// <param name="isOnline">اینکه آیا کاربر آنلاین است یا خیر.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        public async Task UpdateOnlineStatus(bool isOnline)
        {
            try
            {
                await _userService.UpdateLastConnectionDateAsync(UserId, DateTime.UtcNow);

                // Notify all contacts about the status change
                // This would require getting user's contacts first
                await Clients.Others.SendAsync("UserStatusChanged", new { userId = UserId, isOnline, lastSeen = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "هنگام به‌روزرسانی وضعیت خطا رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// وضعیت تایپ کاربر را اطلاع می‌دهد.
        /// </summary>
        /// <param name="chatId">شناسه گفتگو.</param>
        /// <param name="isTyping">وضعیت اینکه آیا در حال تایپ است یا خیر.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        public async Task UpdateTypingStatus(string chatId, bool isTyping)
        {
            try
            {
                // Get chat participants to notify them
                var chatParticipants = await _chatService.GetChatParticipantsAsync(chatId);

                var otherParticipants = chatParticipants.Where(p => p != UserId);
                foreach (var participant in otherParticipants)
                {
                    await Clients.User(participant).SendAsync("UserTyping", new { chatId, userId = UserId, isTyping });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "هنگام به‌روزرسانی وضعیت تایپ خطا رخ داد!", errorDetails = ex.Message });
            }
        }
    }
}