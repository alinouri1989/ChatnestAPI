using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatNest.API.Hubs
{
    /// <summary>
    /// کلاس هاب SignalR برای مدیریت عملیات اعلان در زمان واقعی.
    /// اتصالات کاربر، به‌روزرسانی‌های پروفایل و جستجوی کاربران را مدیریت می‌کند.
    /// </summary>
    [Authorize]
    public sealed class NotificationHub : Hub
    {
        private readonly IUserService _userService;

        /// <summary>
        /// شناسه کاربر فعلی (UserId) را برمی‌گرداند.
        /// شناسه کاربر از مقدار <see cref="ClaimTypes.NameIdentifier"/> در JWT گرفته می‌شود.
        /// </summary>
        /// <returns>شناسه منحصربه‌فرد کاربر فعلی.</returns>
        /// <exception cref="NullReferenceException">
        /// در صورتی که شناسه کاربر یافت نشود یا با مقدار null مواجه شود پرتاب می‌شود.
        /// </exception>
        private string UserId
        {
            get
            {
                var identity = Context?.User?.Identity as ClaimsIdentity;
                return identity?
                    .FindFirst(ClaimTypes.NameIdentifier)?
                    .Value!;
            }
        }

        /// <summary>
        /// یک نمونه جدید از کلاس <see cref="NotificationHub"/> را ایجاد می‌کند.
        /// </summary>
        /// <param name="userService">وابستگی <see cref="IUserService"/> برای عملیات کاربر.</param>
        public NotificationHub(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// زمانی که کاربر اتصال برقرار می‌کند فراخوانی می‌شود.
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="Exception">در صورت بروز خطا هنگام برقراری اتصال پرتاب می‌شود.</exception>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// زمانی که کاربر اتصال را قطع می‌کند فراخوانی می‌شود. تاریخ آخرین اتصال کاربر را به‌روزرسانی کرده و به کاربران دیگر اطلاع می‌دهد.
        /// </summary>
        /// <param name="exception">خطای رخ داده هنگام قطع اتصال (در صورت وجود).</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="Exception">در صورت بروز خطا هنگام قطع اتصال پرتاب می‌شود.</exception>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            DateTime lastConnectionDate = DateTime.UtcNow;
            await _userService.UpdateLastConnectionDateAsync(UserId, lastConnectionDate);

            await Clients.Others.SendAsync("ReceiveRecipientProfiles", new Dictionary<string, Dictionary<string, DateTime>> { { UserId, new Dictionary<string, DateTime> { { "lastConnectionDate", lastConnectionDate } } } });
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// تاریخ آخرین اتصال کاربر را با مقدار صفر به‌روزرسانی می‌کند و به سایر کلاینت‌ها اطلاع می‌دهد.
        /// </summary>
        /// <returns>یک شیء <see cref="Task"/> که عملیات ناهمزمان را نمایندگی می‌کند.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task Initial()
        {
            try
            {
                DateTime lastConnectionDate = DateTime.MinValue;
                await _userService.UpdateLastConnectionDateAsync(UserId, lastConnectionDate!);

                await Clients.Others.SendAsync("ReceiveRecipientProfiles", new Dictionary<string, Dictionary<string, DateTime>>
                {
                    { UserId, new Dictionary<string, DateTime> { { "lastConnectionDate", lastConnectionDate } } }
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("UnexpectedError", new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// در بین کاربران جستجو انجام می‌دهد و نتایج را به کاربر فراخوان‌کننده ارسال می‌کند.
        /// </summary>
        /// <param name="query">عبارت جستجو.</param>
        /// <returns>یک شیء <see cref="Task"/> برمی‌گرداند.</returns>
        /// <exception cref="NotFoundException">در صورت یافت نشدن نتایج جستجو پرتاب می‌شود.</exception>
        /// <exception cref="BadRequestException">در صورت ارائه پارامتر جستجوی نامعتبر پرتاب می‌شود.</exception>
        /// <exception cref="ForbiddenException">در صورتی که کاربر مجاز به انجام جستجو نباشد پرتاب می‌شود.</exception>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        public async Task SearchUsers(string query)
        {
            try
            {
                var users = await _userService.SearchUsersAsync(UserId, query);
                await Clients.Caller.SendAsync("ReceiveSearchUsers", new Dictionary<string, object>
                    {
                        {"query", query },
                        {"data", users }
                    }
                );
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
    }
}