using AutoMapper;
using ChatNest.Entities.Enums;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs;
using ChatNest.Shared.DTOs.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Globalization;
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
        private readonly IMapper _mapper;

        private static readonly ConcurrentDictionary<string, PendingUploadSession> PendingUploads = new();
        private static readonly TimeSpan PendingUploadTtl = TimeSpan.FromMinutes(20);
        private const long MaxUploadBytes = 200L * 1024 * 1024;

        private sealed class PendingUploadSession : IDisposable
        {
            public required string Id { get; init; }
            public required string OwnerUserId { get; init; }
            public required string ChatType { get; init; }
            public required string ChatId { get; init; }
            public required MessageContent ContentType { get; init; }
            public required string FileName { get; init; }
            public required string TempFilePath { get; init; }
            public string? ClientMessageId { get; init; }
            public string? ReplyToMessageId { get; init; }
            public long BytesWritten { get; set; }
            public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
            public SemaphoreSlim SyncLock { get; } = new(1, 1);

            public void Dispose()
            {
                SyncLock.Dispose();
                try
                {
                    if (File.Exists(TempFilePath))
                    {
                        File.Delete(TempFilePath);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }
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
                var userId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? identity?.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userId))
                    throw new UnauthorizedAccessException("User ID not found in token");

                return userId;
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

        /// <summary>
        /// یک نمونه جدید از کلاس <see cref="ChatHub"/> را ایجاد می‌کند.
        /// </summary>
        /// <param name="messageService">وابستگی <see cref="IMessageService"/> برای عملیات پیام.</param>
        /// <param name="groupService">وابستگی <see cref="IGroupService"/> برای عملیات گروه.</param>
        /// <param name="chatService">وابستگی <see cref="IChatService"/> برای عملیات گفتگو.</param>
        /// <param name="userService">وابستگی <see cref="IUserService"/> برای عملیات کاربر.</param>
        public ChatHub(IMessageService messageService, IGroupService groupService, IChatService chatService, IUserService userService, IMapper mapper)
        {
            _messageService = messageService;
            _groupService = groupService;
            _chatService = chatService;
            _userService = userService;
            _mapper = mapper;
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
                CleanupExpiredPendingUploads();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while cleaning up pending uploads: {ex.Message}");
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
        public async Task Initial(int skip = 0, int take = 5)
        {
            try
            {
                var (chats, chatsRecipientIds, userGroupIds) = await _chatService.GetAllChatsAsync(UserId, skip, take);

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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش بارگذاری اولیه گفتگوها",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// دریافت مجموع چت های کاربر
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> که عملیات ناهمزمان را نمایندگی می‌کند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task TotalChatList()
        {
            try
            {
                var totalChats = await _chatService.GetUserChatsCountAsync(UserId);

                await Clients.Caller.SendAsync("ReceiveTotalChats", totalChats);
            }
            catch (Exception ex)
            {
                await SendEmptyDataToClient();
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش دریافت مجموع گفتگوها",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// دریافت مجموع پیام های چت کاربر
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> که عملیات ناهمزمان را نمایندگی می‌کند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task GetTotalMessageCountAsync(string chatId)
        {
            try
            {
                var _chatId = ParseRequiredGuid(chatId, "chat id");
                var participants = await _chatService.GetChatParticipantsAsync(chatId);
                if (!participants.Contains(UserId))
                    throw new NotFoundException("Chat not found or access denied");
                var totalChatMessages = await _messageService.GetTotalMessageCountAsync(_chatId);

                await Clients.Caller.SendAsync("ReceiveTotalChatMessages", totalChatMessages);
            }
            catch (Exception ex) when (ex is NotFoundException || ex is BadRequestException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await SendEmptyDataToClient();
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش شمارش پیام‌های گفتگو",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// دریافت پیام های چت کاربر
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> که عملیات ناهمزمان را نمایندگی می‌کند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task GetChatMessagesAsync(string chatId, int skip = 0, int take = 5)
        {
            try
            {
                var _chatId = ParseRequiredGuid(chatId, "chat id");
                var participants = await _chatService.GetChatParticipantsAsync(chatId);
                if (!participants.Contains(UserId))
                    throw new NotFoundException("Chat not found or access denied");
                var total = await _messageService.GetTotalMessageCountAsync(_chatId);
                var msgs = await _messageService.GetChatMessagesAsync(_chatId, skip, take);

                var response = new ChatMessagesResponse(total, msgs);
                await Clients.Caller.SendAsync("ReceiveChatMessages", response);
            }
            catch (Exception ex) when (ex is NotFoundException || ex is BadRequestException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await SendEmptyDataToClient();
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش دریافت پیام‌های گفتگو",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// دریافت پیام های چت کاربر به صورت روزانه (جدیدترین روز تا قدیمی تر)
        /// </summary>
        public async Task GetChatMessagesByDayAsync(string chatId, string? beforeUtc = null)
        {
            try
            {
                var _chatId = ParseRequiredGuid(chatId, "chat id");
                DateTime? cursorUtc = null;

                if (!string.IsNullOrWhiteSpace(beforeUtc) &&
                    DateTime.TryParse(beforeUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out var parsed))
                {
                    cursorUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                }

                var response = await _messageService.GetChatMessagesByDayAsync(UserId, _chatId, cursorUtc);
                await Clients.Caller.SendAsync("ReceiveChatMessages", response);
            }
            catch (Exception ex) when (ex is NotFoundException || ex is BadRequestException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await SendEmptyDataToClient();
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش دریافت پیام‌ها بر اساس روز",
                    errorDetails = ex.Message
                });
            }
        }

        private bool TryGetOwnedPendingUpload(string uploadId, out PendingUploadSession session)
        {
            session = null!;

            if (string.IsNullOrWhiteSpace(uploadId))
                return false;

            if (!PendingUploads.TryGetValue(uploadId, out var found))
                return false;

            if (!string.Equals(found.OwnerUserId, UserId, StringComparison.Ordinal))
            {
                return false;
            }

            if (DateTime.UtcNow - found.LastActivityUtc > PendingUploadTtl)
            {
                if (PendingUploads.TryRemove(uploadId, out var expiredSession))
                {
                    expiredSession.Dispose();
                }
                return false;
            }

            session = found;
            return true;
        }

        private static void CleanupExpiredPendingUploads()
        {
            var staleUploadIds = PendingUploads
                .Where(item => DateTime.UtcNow - item.Value.LastActivityUtc > PendingUploadTtl)
                .Select(item => item.Key)
                .ToList();

            foreach (var uploadId in staleUploadIds)
            {
                if (PendingUploads.TryRemove(uploadId, out var session))
                {
                    session.Dispose();
                }
            }
        }

        private Dictionary<string, Dictionary<string, ChatDto>> CreateEmptyChatsStructure()
        {
            return new Dictionary<string, Dictionary<string, ChatDto>>
                {
                    { "Individual", new Dictionary<string, ChatDto>() },
                    { "Group", new Dictionary<string, ChatDto>() }
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

            await Clients.Caller.SendAsync("ReceiveInitialChats", _mapper.Map<ChatDto>(emptyChats));
            await Clients.Caller.SendAsync("ReceiveInitialGroupProfiles", emptyGroupProfiles);
            await Clients.Caller.SendAsync("ReceiveInitialRecipientChatProfiles", emptyRecipientProfiles);
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
                await Clients.User(participant).SendAsync("ReceiveCreateChat", chatResponse);
            }

            var recipientProfiles = await _userService.GetRecipientProfilesAsync(chatParticipants);
            foreach (var participant in chatParticipants)
            {
                var profileUserId = chatParticipants.FirstOrDefault(p => p != participant) ?? participant;
                if (!recipientProfiles.TryGetValue(profileUserId, out var profileData))
                {
                    continue;
                }

                var profileResponse = new Dictionary<string, object>
                {
                    { profileUserId, profileData }
                };
                await Clients.User(participant).SendAsync("ReceiveRecipientProfiles", profileResponse);
            }

            return chatId;
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
                    await BroadcastIndividualChatAsync(chat);
                }
                else if (chatType.Equals("Group", StringComparison.OrdinalIgnoreCase))
                {
                    // For group chats, recipientId would be the groupId
                    var groupParticipants = await _groupService.GetGroupParticipantsAsync(UserId, recipientId);

                    var chatResponse = new Dictionary<string, Dictionary<string, ChatDto>> { { "Group", chat } };
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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش ایجاد گفتگو",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// چت شخصی کاربر (Saved Messages) را ایجاد یا بازیابی می‌کند.
        /// </summary>
        /// <returns>شناسه چت Saved Messages.</returns>
        public async Task<string> GetOrCreateSavedMessagesChat()
        {
            try
            {
                var chat = await _chatService.CreateChatAsync(UserId, "Individual", UserId);
                var chatId = await BroadcastIndividualChatAsync(chat);
                return chatId ?? string.Empty;
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
                return string.Empty;
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش گفتگوی پیام‌های ذخیره‌شده",
                    errorDetails = ex.Message
                });
                return string.Empty;
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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش پاکسازی گفتگو",
                    errorDetails = ex.Message
                });
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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش آرشیو گفتگو",
                    errorDetails = ex.Message
                });
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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش خارج‌سازی گفتگو از آرشیو",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// ایجاد یک نشست آپلود فایل برای ارسال chunk ای.
        /// </summary>
        public Task<string> BeginFileUpload(
            string chatType,
            string chatId,
            int contentType,
            string fileName,
            string? clientMessageId = null,
            string? replyToMessageId = null)
        {
            if (string.IsNullOrWhiteSpace(chatType))
                throw new BadRequestException("ChatType is required");

            if (!Enum.IsDefined(typeof(MessageContent), contentType))
                throw new BadRequestException("Invalid message type");

            var parsedContentType = (MessageContent)contentType;
            if (parsedContentType == MessageContent.Text)
                throw new BadRequestException("Text messages do not require file upload");

            if (string.IsNullOrWhiteSpace(chatId))
                throw new BadRequestException("ChatId is required");

            var uploadId = Guid.NewGuid().ToString("N");
            var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "attachment" : fileName.Trim();
            CleanupExpiredPendingUploads();

            var uploadRoot = Path.Combine(Path.GetTempPath(), "chatnest_uploads");
            Directory.CreateDirectory(uploadRoot);
            var tempFilePath = Path.Combine(uploadRoot, $"{uploadId}.tmp");

            var session = new PendingUploadSession
            {
                Id = uploadId,
                OwnerUserId = UserId,
                ChatType = chatType,
                ChatId = chatId,
                ContentType = parsedContentType,
                FileName = safeFileName,
                ClientMessageId = clientMessageId,
                ReplyToMessageId = replyToMessageId,
                TempFilePath = tempFilePath
            };

            using (File.Create(tempFilePath))
            {
                // Ensure file is present and empty.
            }

            PendingUploads[uploadId] = session;
            return Task.FromResult(uploadId);
        }

        /// <summary>
        /// دریافت یک chunk از فایل (base64) و افزودن به نشست آپلود.
        /// </summary>
        public async Task UploadFileChunk(string uploadId, string base64Chunk)
        {
            if (!TryGetOwnedPendingUpload(uploadId, out var session))
                throw new NotFoundException("Upload session not found");

            if (string.IsNullOrWhiteSpace(base64Chunk))
                throw new BadRequestException("Chunk payload is required");

            byte[] chunkBytes;
            try
            {
                chunkBytes = Convert.FromBase64String(base64Chunk);
            }
            catch (FormatException)
            {
                throw new BadRequestException("Invalid chunk payload");
            }

            if (chunkBytes.Length == 0)
                return;

            PendingUploadSession? oversizedSession = null;
            await session.SyncLock.WaitAsync();
            try
            {
                if (session.BytesWritten + chunkBytes.Length > MaxUploadBytes)
                {
                    PendingUploads.TryRemove(uploadId, out oversizedSession);
                    throw new BadRequestException("File size exceeds the allowed limit (200MB)");
                }

                await using var fileStream = new FileStream(
                    session.TempFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                await fileStream.WriteAsync(chunkBytes, 0, chunkBytes.Length);
                session.BytesWritten += chunkBytes.Length;
                session.LastActivityUtc = DateTime.UtcNow;

                if (session.BytesWritten > MaxUploadBytes)
                {
                    PendingUploads.TryRemove(uploadId, out oversizedSession);
                    throw new BadRequestException("File size exceeds the allowed limit (200MB)");
                }
            }
            catch (Exception ex) when (ex is NotFoundException || ex is ForbiddenException || ex is BadRequestException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش ارسال بخش فایل (Chunk Upload)",
                    errorDetails = ex.Message
                });
            }
            finally
            {
                session.SyncLock.Release();
                oversizedSession?.Dispose();
            }
        }

        /// <summary>
        /// تکمیل آپلود chunk ای و ارسال پیام نهایی به شرکت‌کنندگان چت.
        /// </summary>
        public async Task CompleteFileUpload(string uploadId)
        {
            if (!TryGetOwnedPendingUpload(uploadId, out _))
                throw new NotFoundException("Upload session not found");

            if (!PendingUploads.TryRemove(uploadId, out var removedSession))
                throw new NotFoundException("Upload session not found");

            try
            {
                await removedSession.SyncLock.WaitAsync();

                removedSession.LastActivityUtc = DateTime.UtcNow;

                if (removedSession.BytesWritten <= 0 || !File.Exists(removedSession.TempFilePath))
                    throw new BadRequestException("Uploaded file is empty");

                var fileBytes = await File.ReadAllBytesAsync(removedSession.TempFilePath);
                if (fileBytes.LongLength == 0)
                    throw new BadRequestException("Uploaded file is empty");

                var dto = new SendMessage
                {
                    ContentType = removedSession.ContentType,
                    Content = "__chunk_upload__",
                    ClientMessageId = removedSession.ClientMessageId,
                    FileName = removedSession.FileName,
                    File = fileBytes,
                    ReplyToMessageId = removedSession.ReplyToMessageId,
                    ThumbnailUrl = ""
                };

                var (message, chatParticipants) = await _messageService.SendMessageAsync(
                    UserId,
                    removedSession.ChatId,
                    removedSession.ChatType,
                    dto);

                foreach (var participant in chatParticipants)
                {
                    await Clients.User(participant).SendAsync("ReceiveGetMessages", message);
                }

                if (removedSession.SyncLock.CurrentCount == 0)
                {
                    removedSession.SyncLock.Release();
                }
                removedSession.Dispose();
            }
            catch (Exception ex) when (ex is NotFoundException || ex is ForbiddenException || ex is BadRequestException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش تکمیل آپلود فایل",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// لغو نشست آپلود فایل و آزادسازی منابع مربوطه.
        /// </summary>
        public Task AbortFileUpload(string uploadId)
        {
            if (!TryGetOwnedPendingUpload(uploadId, out _))
                return Task.CompletedTask;

            if (PendingUploads.TryRemove(uploadId, out var session))
            {
                session.Dispose();
            }

            return Task.CompletedTask;
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
            catch (Exception ex) when (ex is BadRequestException || ex is NotFoundException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش ارسال پیام",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// فوروارد کردن یک پیوست موجود به گفتگوی دیگر بدون آپلود مجدد فایل.
        /// </summary>
        public async Task ForwardAttachment(string targetChatType, string targetChatId, string sourceMessageId)
        {
            try
            {
                var (message, chatParticipants) = await _messageService.ForwardAttachmentAsync(
                    UserId,
                    sourceMessageId,
                    targetChatId,
                    targetChatType);

                foreach (var participant in chatParticipants)
                {
                    await Clients.User(participant).SendAsync("ReceiveGetMessages", message);
                }
            }
            catch (Exception ex) when (ex is BadRequestException || ex is NotFoundException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش فوروارد فایل",
                    errorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// فوروارد کردن پیوست به یک کاربر؛ اگر گفتگوی شخصی وجود نداشته باشد ابتدا ساخته می‌شود.
        /// </summary>
        public async Task ForwardAttachmentToUser(string recipientId, string sourceMessageId)
        {
            try
            {
                var chat = await _chatService.CreateChatAsync(UserId, "Individual", recipientId);
                var targetChatId = await BroadcastIndividualChatAsync(chat);

                if (string.IsNullOrWhiteSpace(targetChatId))
                    throw new BadRequestException("Target chat could not be resolved");

                var (message, chatParticipants) = await _messageService.ForwardAttachmentAsync(
                    UserId,
                    sourceMessageId,
                    targetChatId,
                    "Individual");

                foreach (var participant in chatParticipants)
                {
                    await Clients.User(participant).SendAsync("ReceiveGetMessages", message);
                }
            }
            catch (Exception ex) when (ex is BadRequestException || ex is NotFoundException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش فوروارد فایل به کاربر",
                    errorDetails = ex.Message
                });
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
            catch (Exception ex) when (ex is NotFoundException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش تحویل پیام",
                    errorDetails = ex.Message
                });
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
            catch (Exception ex) when (ex is NotFoundException || ex is ForbiddenException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش خواندن پیام",
                    errorDetails = ex.Message
                });
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
            catch (Exception ex) when (ex is NotFoundException || ex is ForbiddenException || ex is BadRequestException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش حذف پیام",
                    errorDetails = ex.Message
                });
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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش عضویت در گروه",
                    errorDetails = ex.Message
                });
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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش خروج از گروه",
                    errorDetails = ex.Message
                });
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
                var lastConnectionDate = isOnline ? DateTime.MinValue : DateTime.UtcNow;
                await _userService.UpdateLastConnectionDateAsync(UserId, lastConnectionDate);

                // Notify all contacts about the status change
                // This would require getting user's contacts first
                await Clients.Others.SendAsync("UserStatusChanged", new { userId = UserId, isOnline, lastSeen = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش به‌روزرسانی وضعیت آنلاین",
                    errorDetails = ex.Message
                });
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
                await Clients.Caller.SendAsync("UnexpectedError", new
                {
                    message = "خطای غیرمنتظره در بخش وضعیت تایپ کاربر",
                    errorDetails = ex.Message
                });
            }
        }
    }
}
