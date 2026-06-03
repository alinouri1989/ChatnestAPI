using ChatNest.Entities.Enums;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;

namespace ChatNest.API.Hubs
{
    /// <summary>
    /// کلاس هاب SignalR برای مدیریت عملیات تماس در زمان واقعی.
    /// اتصالات کاربر، شروع تماس، پایان تماس و صدور توکن LiveKit را مدیریت می‌کند.
    /// </summary>
    [Authorize]
    public sealed class CallHub : Hub
    {
        private readonly IUserService _userService;
        private readonly ICallService _callService;
        private readonly IUserPresenceTracker _presenceTracker;
        private readonly IConfiguration _configuration;

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
            IUserPresenceTracker presenceTracker,
            IConfiguration configuration)
        {
            _userService = userService;
            _callService = callService;
            _presenceTracker = presenceTracker;
            _configuration = configuration;
        }

        private void ApplyPresenceState(Dictionary<string, ChatNest.Shared.DTOs.Response.CallerUser> profiles)
        {
            foreach (var (userId, profile) in profiles)
            {
                profile.IsOnline = _presenceTracker.IsOnline(userId);
            }
        }

        private async Task SendCallerErrorAsync(string eventName, string message, string? errorDetails = null)
        {
            try
            {
                await Clients.Caller.SendAsync(eventName, new { message, errorDetails });
            }
            catch
            {
                // Avoid converting an error notification failure into a hub invocation exception.
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
        public async Task StartCall(string recipientId, int callType)
        {
            try
            {
                if (!Enum.IsDefined(typeof(CallType), callType))
                {
                    throw new BadRequestException("Invalid call type");
                }

                var parsedCallType = (CallType)callType;
                var callId = await _callService.StartCallAsync(UserId, recipientId, parsedCallType);
                List<string> callParticipants = [UserId, recipientId];
                var recipientProfiles = await _userService.GetUserProfilesAsync(callParticipants);
                ApplyPresenceState(recipientProfiles);

                recipientProfiles.TryGetValue(recipientId, out var recipientProfile);
                recipientProfiles.TryGetValue(UserId, out var callerProfile);

                await Clients.User(UserId).SendAsync("ReceiveOutgoingCall", new
                {
                    callId,
                    callType = (int)parsedCallType,
                    callerId = UserId,
                    recipientId,
                    profileUserId = recipientId,
                    profile = recipientProfile
                });

                await Clients.User(recipientId).SendAsync("ReceiveIncomingCall", new
                {
                    callId,
                    callType = (int)parsedCallType,
                    callerId = UserId,
                    recipientId,
                    profileUserId = UserId,
                    profile = callerProfile
                });
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException)
            {
                await SendCallerErrorAsync("ValidationError", ex.Message);
            }
            catch (Exception ex)
            {
                await SendCallerErrorAsync("UnexpectedError", "خطای غیرمنتظره‌ای در شروع تماس رخ داد!", ex.Message);
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

        public async Task<object> CreateLiveKitJoinToken(string callId, string? displayName)
        {
            try
            {
                var call = await _callService.GetCallAsync(UserId, callId);
                var participants = await _callService.GetCallParticipantsAsync(UserId, callId);

                var serverUrl = _configuration["LiveKit:ServerUrl"];
                var apiKey = _configuration["LiveKit:ApiKey"];
                var apiSecret = _configuration["LiveKit:ApiSecret"];
                var roomPrefix = _configuration["LiveKit:RoomPrefix"] ?? "chatnest-call";
                var ttlMinutes = _configuration.GetValue<int?>("LiveKit:TokenTtlMinutes") ?? 30;

                if (string.IsNullOrWhiteSpace(serverUrl) ||
                    string.IsNullOrWhiteSpace(apiKey) ||
                    string.IsNullOrWhiteSpace(apiSecret))
                {
                    throw new InvalidOperationException("LiveKit configuration is incomplete.");
                }

                var now = DateTimeOffset.UtcNow;
                var expires = now.AddMinutes(ttlMinutes <= 0 ? 30 : ttlMinutes);
                var roomName = $"{roomPrefix}-{call.Id:N}";
                var identity = $"chatnest-{UserId}";
                var videoGrant = new Dictionary<string, object>
                {
                    ["roomJoin"] = true,
                    ["room"] = roomName,
                    ["canPublish"] = true,
                    ["canSubscribe"] = true,
                    ["canPublishData"] = true,
                    ["canUpdateOwnMetadata"] = true
                };

                var metadata = new
                {
                    callId = call.Id,
                    callType = call.Type.ToString(),
                    userId = UserId,
                    participants
                };

                var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(apiSecret));
                var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
                var payload = new JwtPayload
                {
                    ["iss"] = apiKey,
                    ["sub"] = identity,
                    ["name"] = string.IsNullOrWhiteSpace(displayName) ? identity : displayName,
                    ["metadata"] = JsonSerializer.Serialize(metadata),
                    ["nbf"] = now.ToUnixTimeSeconds(),
                    ["exp"] = expires.ToUnixTimeSeconds(),
                    ["video"] = videoGrant
                };

                var token = new JwtSecurityToken(new JwtHeader(credentials), payload);

                return new
                {
                    serverUrl,
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    roomName,
                    identity,
                    expiresAt = expires.UtcDateTime,
                    callId = call.Id,
                    callType = call.Type
                };
            }
            catch (Exception ex) when (
                ex is NotFoundException ||
                ex is BadRequestException ||
                ex is ForbiddenException ||
                ex is InvalidOperationException)
            {
                await Clients.Caller.SendAsync("ValidationError", new { message = ex.Message });
                throw;
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
        public async Task EndCall(string callId, int callStatus, DateTime? createdDate)
        {
            try
            {
                if (!Enum.IsDefined(typeof(CallStatus), callStatus))
                {
                    throw new BadRequestException("Invalid call status");
                }

                var call = await _callService.EndCallAsync(UserId, callId, (CallStatus)callStatus, createdDate);
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

    }
}
