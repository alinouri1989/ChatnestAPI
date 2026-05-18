using ChatNest.Entities.Enums;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatNest.API.Hubs
{
    /// <summary>
    /// کلاس هاب SignalR برای مدیریت عملیات تماس در زمان واقعی.
    /// اتصالات کاربر، شروع تماس، پایان تماس و عملیات سیگنالینگ WebRTC را مدیریت می‌کند.
    /// </summary>
    [Authorize]
    public sealed class CallHub : Hub
    {
        private readonly IUserService _userService;
        private readonly ICallService _callService;
        private readonly IUserPresenceTracker _presenceTracker;

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

        /// <summary>
        /// یک نمونه جدید از کلاس <see cref="CallHub"/> را ایجاد می‌کند.
        /// </summary>
        /// <param name="userService">وابستگی <see cref="IUserService"/> برای عملیات کاربر.</param>
        /// <param name="callService">وابستگی <see cref="ICallService"/> برای عملیات تماس.</param>
        public CallHub(
            IUserService userService,
            ICallService callService,
            IUserPresenceTracker presenceTracker)
        {
            _userService = userService;
            _callService = callService;
            _presenceTracker = presenceTracker;
        }

        private void ApplyPresenceState(Dictionary<string, ChatNest.Shared.DTOs.Response.CallerUser> profiles)
        {
            foreach (var (userId, profile) in profiles)
            {
                profile.IsOnline = _presenceTracker.IsOnline(userId);
            }
        }

        /// <summary>
        /// زمانی که اتصال کاربر برقرار می‌شود فراخوانی می‌شود.
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
                await Clients.Caller.SendAsync("ConnectionError", new { message = "خطای اتصال به هاب تماس رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// زمانی که اتصال کاربر قطع می‌شود فراخوانی می‌شود. دلیل قطع اتصال می‌تواند به صورت اختیاری با یک <see cref="Exception"/> ارائه شود.
        /// </summary>
        /// <param name="exception">خطایی که باعث قطع اتصال شده است (در صورت وجود).</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var (calls, _) = await _callService.GetCallLogsAsync(UserId);

                foreach (var callCategory in calls.Values)
                {
                    foreach (var call in callCategory)
                    {
                        if (call.Value.Status.Equals(CallStatus.Ongoing))
                        {
                            var endCall = await _callService.EndCallAsync(UserId, call.Key, CallStatus.Accepted, DateTime.UtcNow);
                            var callParticipants = await _callService.GetCallParticipantsAsync(UserId, call.Key);
                            var recipientProfiles = await _userService.GetUserProfilesAsync(callParticipants);
                            ApplyPresenceState(recipientProfiles);

                            foreach (var participant in callParticipants)
                            {
                                // Find the other participant (not current user)
                                var otherParticipant = callParticipants.FirstOrDefault(p => p != UserId);
                                if (!string.IsNullOrEmpty(otherParticipant))
                                {
                                    var profileToSend = participant.Equals(UserId) ?
                                        (recipientProfiles.ContainsKey(otherParticipant) ? recipientProfiles[otherParticipant] : null) :
                                        (recipientProfiles.ContainsKey(UserId) ? recipientProfiles[UserId] : null);

                                    if (profileToSend != null)
                                    {
                                        await Clients.User(participant).SendAsync("ReceiveEndCall", new Dictionary<string, object>
                                        {
                                            { "call", endCall },
                                            { participant.Equals(UserId) ? otherParticipant : UserId, profileToSend }
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't send to client since they're disconnecting
                Console.WriteLine($"Error handling ongoing calls in CallHub disconnect: {ex.Message}");
            }
            finally
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        /// <summary>
        /// تاریخچه تماس کاربر و پروفایل‌های گیرنده مرتبط را بارگذاری کرده و به کلاینت ارسال می‌کند.
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> که عملیات ناهمزمان را نمایندگی می‌کند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task Initial()
        {
            try
            {
                var (calls, callRecipientIds) = await _callService.GetCallLogsAsync(UserId);
                var recipientProfiles = await _userService.GetUserProfilesAsync(callRecipientIds);
                ApplyPresenceState(recipientProfiles);

                await Clients.Caller.SendAsync("ReceiveInitialCalls", calls);
                await Clients.Caller.SendAsync("ReceiveInitialCallRecipientProfiles", recipientProfiles);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای در بارگذاری تماس‌ها رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// یک تماس جدید بین کاربران شروع می‌کند و اطلاعات تماس را به هر دو کاربر (تماس‌گیرنده و گیرنده) ارسال می‌کند.
        /// </summary>
        /// <param name="recipientId">شناسه منحصربه‌فرد کاربری که گیرنده تماس است.</param>
        /// <param name="callType">نوع تماس را مشخص می‌کند (صوتی یا تصویری).</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن کاربر یا گیرنده پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به شروع تماس نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task StartCall(string recipientId, CallType callType)
        {
            try
            {
                var callId = await _callService.StartCallAsync(UserId, recipientId, callType);
                List<string> callParticipants = [UserId, recipientId];
                var recipientProfiles = await _userService.GetUserProfilesAsync(callParticipants);
                ApplyPresenceState(recipientProfiles);

                await Clients.User(UserId).SendAsync("ReceiveOutgoingCall", new Dictionary<string, object>
                {
                    { "callId", callId },
                    { "callType", callType },
                    { "callerId", UserId },
                    { "recipientId", recipientId },
                    { recipientId, recipientProfiles.ContainsKey(recipientId) ? recipientProfiles[recipientId] : null }
                });

                await Clients.User(recipientId).SendAsync("ReceiveIncomingCall", new Dictionary<string, object>
                {
                    { "callId", callId },
                    { "callType", callType },
                    { "callerId", UserId },
                    { "recipientId", recipientId },
                    { UserId, recipientProfiles.ContainsKey(UserId) ? recipientProfiles[UserId] : null }
                });
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای در شروع تماس رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// تماس مشخص شده را قبول می‌کند و اعلان قبول تماس را به کلاینت ارسال می‌کند.
        /// </summary>
        /// <param name="callId">شناسه تماسی که باید قبول شود.</param>
        /// <returns>زمانی که تماس قبول شود بازخورد به کلاینت ارسال می‌کند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن تماس پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">زمانی که درخواست نامعتبری ارسال شود پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورت دسترسی غیرمجاز پرتاب می‌شود.</exception>
        /// <exception cref="Exception">زمانی که خطای غیرمنتظره‌ای رخ دهد پرتاب می‌شود.</exception>
        public async Task AcceptCall(string callId)
        {
            try
            {
                await _callService.AcceptCallAsync(UserId, callId);
                await Clients.User(UserId).SendAsync("ReceiveAcceptCall", true);
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای در پذیرش تماس رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// تماس مشخص شده را پایان می‌دهد و به تمام شرکت‌کنندگان اطلاع پایان تماس را ارسال می‌کند.
        /// </summary>
        /// <param name="callId">شناسه تماسی که باید پایان یابد.</param>
        /// <param name="callStatus">وضعیت پایان تماس.</param>
        /// <param name="createdDate">تاریخ ایجاد تماس (اختیاری).</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن تماس پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به پایان دادن تماس نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task EndCall(string callId, CallStatus callStatus, DateTime? createdDate)
        {
            try
            {
                var call = await _callService.EndCallAsync(UserId, callId, callStatus, createdDate);
                var callParticipants = await _callService.GetCallParticipantsAsync(UserId, callId);
                var recipientProfiles = await _userService.GetUserProfilesAsync(callParticipants);

                foreach (var participant in callParticipants)
                {
                    // Find the other participant (not current participant)
                    var otherParticipant = callParticipants.FirstOrDefault(p => p != participant);
                    if (!string.IsNullOrEmpty(otherParticipant))
                    {
                        var profileToSend = recipientProfiles.ContainsKey(otherParticipant) ? recipientProfiles[otherParticipant] : null;

                        if (profileToSend != null)
                        {
                            await Clients.User(participant).SendAsync("ReceiveEndCall", new Dictionary<string, object>
                            {
                                { "call", call },
                                { otherParticipant, profileToSend }
                            });
                        }
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای در پایان تماس رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// تماس مشخص شده توسط کاربر را حذف می‌کند و اعلان را به کلاینت ارسال می‌کند.
        /// </summary>
        /// <param name="callId">شناسه تماسی که باید حذف شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن تماس پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به حذف تماس نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task DeleteCall(string callId)
        {
            try
            {
                await _callService.DeleteCallAsync(UserId, callId);
                await Clients.User(UserId).SendAsync("ReceiveDeleteCall", callId);
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای در حذف تماس رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// اطلاعات SDP (Session Description Protocol) را برای اتصال WebRTC به شرکت‌کننده دیگر تماس ارسال می‌کند.
        /// </summary>
        /// <param name="callId">شناسه تماسی که اطلاعات SDP برای آن ارسال می‌شود.</param>
        /// <param name="sdp">شیء SDP که باید ارسال شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن تماس پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task SendSdp(string callId, object sdp)
        {
            try
            {
                var call = await _callService.GetCallAsync(UserId, callId);
                var participants = await _callService.GetCallParticipantsAsync(UserId, callId);

                foreach (var participant in participants)
                {
                    if (!participant.Equals(UserId))
                    {
                        await Clients.User(participant).SendAsync("ReceiveSdp", new Dictionary<string, object>
                        {
                            {"callId", callId },
                            {"sdp", sdp },
                            {"callType", call.Type }
                        });
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای در ارسال SDP رخ داد!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// نامزدهای ICE (Interactive Connectivity Establishment) را برای اتصال WebRTC به شرکت‌کننده دیگر تماس ارسال می‌کند.
        /// </summary>
        /// <param name="callId">شناسه تماسی که نامزد ICE برای آن ارسال می‌شود.</param>
        /// <param name="iceCandidate">شیء نامزد ICE که باید ارسال شود.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن تماس پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامترهای نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام این عملیات نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task SendIceCandidate(string callId, object iceCandidate)
        {
            try
            {
                var participants = await _callService.GetCallParticipantsAsync(UserId, callId);

                foreach (var participant in participants)
                {
                    if (!participant.Equals(UserId))
                    {
                        await Clients.User(participant).SendAsync("ReceiveIceCandidate", new Dictionary<string, object>
                        {
                            { "callId", callId },
                            { "iceCandidate", iceCandidate }
                        });
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
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای در ارسال ICE Candidate رخ داد!", errorDetails = ex.Message });
            }
        }
    }
}
