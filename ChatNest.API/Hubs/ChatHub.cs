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
    /// Gerçek zamanlı sohbet işlemlerini yöneten SignalR hub sınıfıdır.
    /// Kullanıcı bağlantılarını, sohbet başlatma, mesaj gönderme, sohbetleri listeleme ve grup işlemleri gibi işlemleri yönetir.
    /// </summary>
    [Authorize]
    public sealed class ChatHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IGroupService _groupService;
        private readonly IChatService _chatService;
        private readonly IUserService _userService;

        /// <summary>
        /// Geçerli kullanıcının kimliğini (UserId) döndürür.
        /// Kullanıcının kimliği, JWT içindeki <see cref="ClaimTypes.NameIdentifier"/> değerinden alınır.
        /// </summary>
        /// <returns>Geçerli kullanıcının benzersiz kimliği.</returns>
        /// <exception cref="UnauthorizedAccessException">
        /// Eğer kullanıcı kimliği bulunamazsa veya bir null değer ile karşılaşılırsa fırlatılır.
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
        /// <see cref="ChatHub"/> sınıfının yeni bir örneğini oluşturur.
        /// </summary>
        /// <param name="messageService">Mesaj işlemleri için <see cref="IMessageService"/> bağımlılığı.</param>
        /// <param name="groupService">Grup işlemleri için <see cref="IGroupService"/> bağımlılığı.</param>
        /// <param name="chatService">Sohbet işlemleri için <see cref="IChatService"/> bağımlılığı.</param>
        /// <param name="userService">Kullanıcı işlemleri için <see cref="IUserService"/> bağımlılığı.</param>
        public ChatHub(IMessageService messageService, IGroupService groupService, IChatService chatService, IUserService userService)
        {
            _messageService = messageService;
            _groupService = groupService;
            _chatService = chatService;
            _userService = userService;
        }

        /// <summary>
        /// Kullanıcı hub'a bağlandığında tetiklenen metod.
        /// </summary>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
        public override async Task OnConnectedAsync()
        {
            try
            {
                await _userService.UpdateLastConnectionDateAsync(UserId, DateTime.UtcNow);
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ConnectionError", new { message = "Bağlantı hatası oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı hub'dan ayrıldığında tetiklenen metod.
        /// </summary>
        /// <param name="exception">Bağlantı sırasında oluşan hata (varsa).</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
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
        /// Kullanıcının tüm sohbetlerini, alıcı profillerini ve grup profillerini yükler ve istemciye iletir.
        /// </summary>
        /// <returns>Asenkron işlemi temsil eden bir <see cref="Task"/> nesnesi.</returns>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
        public async Task Initial()
        {
            try
            {
                var (chats, chatsRecipientIds, userGroupIds) = await _chatService.GetAllChatsAsync(UserId);

                var recipientProfilesTask = _userService.GetRecipientProfilesAsync(chatsRecipientIds);
                var groupProfilesTask = _groupService.GetGroupProfilesAsync(userGroupIds);

                var recipientProfiles = await recipientProfilesTask;
                var groupProfiles = await groupProfilesTask;

                await Clients.Caller.SendAsync("ReceiveInitialChats", chats);
                await Clients.Caller.SendAsync("ReceiveInitialGroupProfiles", groupProfiles);
                await Clients.Caller.SendAsync("ReceiveInitialRecipientChatProfiles", recipientProfiles);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir sohbet başlatır ve katılımcılara sohbet bilgilerini iletir.
        /// </summary>
        /// <param name="chatType">Sohbet tipi ("Individual" veya "Group").</param>
        /// <param name="recipientId">Sohbete katılacak alıcının kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                        var chatParticipants = chatEntity.Participants;

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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Belirli bir sohbetin içeriğini temizler.
        /// </summary>
        /// <param name="chatType">Sohbet tipi ("Individual" veya "Group").</param>
        /// <param name="chatId">Temizlenecek sohbetin kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Belirli bir sohbeti arşivler.
        /// </summary>
        /// <param name="chatId">Arşivlenecek sohbetin kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Belirli bir sohbeti arşivden çıkarır.
        /// </summary>
        /// <param name="chatId">Arşivden çıkarılacak sohbetin kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcıdan gelen mesajı belirtilen sohbetin katılımcılarına gönderir.
        /// </summary>
        /// <param name="chatType">Sohbet tipi ("Individual" veya "Group").</param>
        /// <param name="chatId">Mesajın gönderileceği sohbetin kimliği.</param>
        /// <param name="dto">Gönderilen mesajı temsil eden DTO.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet veya mesaj verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Bir mesajın teslim edildiğini işaretler ve sohbet katılımcılarına bildirir.
        /// </summary>
        /// <param name="chatType">Sohbet tipi ("Individual" veya "Group").</param>
        /// <param name="chatId">Mesajın teslim edileceği sohbetin kimliği.</param>
        /// <param name="messageId">Teslim edilecek mesajın kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet veya mesaj verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Bir mesajın okunduğunu işaretler ve sohbet katılımcılarına bildirir.
        /// </summary>
        /// <param name="chatType">Sohbet tipi ("Individual" veya "Group").</param>
        /// <param name="chatId">Mesajın okunacağı sohbetin kimliği.</param>
        /// <param name="messageId">Okunacak mesajın kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet veya mesaj verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen mesajı siler ve sohbet katılımcılarına bildirir.
        /// </summary>
        /// <param name="chatType">Sohbet tipi ("Individual" veya "Group").</param>
        /// <param name="chatId">Silinecek mesajın bulunduğu sohbetin kimliği.</param>
        /// <param name="messageId">Silinecek mesajın kimliği.</param>
        /// <param name="deletionType">Silme türünü belirtir ("0" veya "1").</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        /// <exception cref="NotFoundException">Sohbet veya mesaj verisi bulunamazsa fırlatılır.</exception>
        /// <exception cref="BadRequestException">Geçersiz parametreler sağlanırsa fırlatılır.</exception>
        /// <exception cref="ForbiddenException">Kullanıcının bu işlemi gerçekleştirme yetkisi yoksa fırlatılır.</exception>
        /// <exception cref="Exception">Beklenmedik bir hata oluşursa fırlatılır.</exception>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Beklenmedik bir hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcının belirli bir gruba katılmasını sağlar.
        /// </summary>
        /// <param name="groupId">Katılınacak grup kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        public async Task JoinGroup(string groupId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Group_{groupId}");
                await Clients.Caller.SendAsync("JoinedGroup", new { groupId });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Gruba katılırken hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcının belirli bir gruptan ayrılmasını sağlar.
        /// </summary>
        /// <param name="groupId">Ayrılınacak grup kimliği.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        public async Task LeaveGroup(string groupId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Group_{groupId}");
                await Clients.Caller.SendAsync("LeftGroup", new { groupId });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Gruptan ayrılırken hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcının çevrimiçi durumunu günceller.
        /// </summary>
        /// <param name="isOnline">Kullanıcının çevrimiçi olup olmadığı.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Durum güncellenirken hata oluştu!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcının yazdığını bildirir.
        /// </summary>
        /// <param name="chatId">Sohbet kimliği.</param>
        /// <param name="isTyping">Yazyıp yazmadığı durumu.</param>
        /// <returns>Bir <see cref="Task"/> nesnesi döner.</returns>
        public async Task UpdateTypingStatus(string chatId, bool isTyping)
        {
            try
            {
                // Get chat participants to notify them
                var (chats, _, _) = await _chatService.GetAllChatsAsync(UserId);

                Chat? targetChat = null;
                foreach (var chatType in chats.Values)
                {
                    if (chatType.ContainsKey(chatId))
                    {
                        targetChat = chatType[chatId];
                        break;
                    }
                }

                if (targetChat != null)
                {
                    var otherParticipants = targetChat.Participants.Where(p => p != UserId);
                    foreach (var participant in otherParticipants)
                    {
                        await Clients.User(participant).SendAsync("UserTyping", new { chatId, userId = UserId, isTyping });
                    }
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "Yazma durumu güncellenirken hata oluştu!", errorDetails = ex.Message });
            }
        }
    }
}