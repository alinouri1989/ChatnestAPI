using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatNest.API.Controllers
{
    /// <summary>
    /// کلاس کنترلر پایه که قابلیت‌های مشترک در درخواست‌های API را مدیریت می‌کند.
    /// تمام کنترلرها از این کلاس ارث‌بری می‌کنند و می‌توانند به شناسه کاربر دسترسی داشته باشند.
    /// </summary>
    [Authorize]
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        /// <summary>
        /// شناسه کاربر فعلی (UserId) را برمی‌گرداند.
        /// شناسه کاربر از مقدار <see cref="ClaimTypes.NameIdentifier"/> در JWT گرفته می‌شود.
        /// </summary>
        /// <returns>شناسه منحصربه‌فرد کاربر فعلی.</returns>
        /// <exception cref="NullReferenceException">
        /// در صورتی که شناسه کاربر یافت نشود یا با مقدار null مواجه شود پرتاب می‌شود.
        /// </exception>
        protected string UserId
        {
            get
            {
                var identity = HttpContext?.User?.Identity as ClaimsIdentity;
                return identity?
                    .FindFirst(ClaimTypes.NameIdentifier)?
                    .Value
                    ?? identity?.FindFirst("sub")?.Value!;
            }
        }
    }
}
