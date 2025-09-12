using ChatNest.Services.Abstract;
using ChatNest.Shared.DTOs.Request;
using Microsoft.AspNetCore.Mvc;

namespace ChatNest.API.Controllers
{
    /// <summary>
    /// کنترلر API برای مدیریت عملیات هوش مصنوعی تولیدی.
    /// شامل تولید متن و تصویر با استفاده از سرویس‌های AI می‌باشد.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    public sealed class GenerativeAiController : BaseController
    {
        private readonly IGenerativeAiService _generativeAiService;

        /// <summary>
        /// یک نمونه جدید از کلاس <see cref="GenerativeAiController"/> را ایجاد می‌کند.
        /// </summary>
        /// <param name="generativeAiService">وابستگی <see cref="IGenerativeAiService"/> برای عملیات هوش مصنوعی تولیدی.</param>
        public GenerativeAiController(IGenerativeAiService generativeAiService)
        {
            _generativeAiService = generativeAiService;
        }

        /// <summary>
        /// متن تولید می‌کند با استفاده از مدل Gemini.
        /// در صورت وجود ورودی‌های نامعتبر یا بروز خطا، پاسخ مناسب برمی‌گرداند.
        /// </summary>
        /// <param name="request">درخواست AI که شامل اطلاعات مورد نیاز برای تولید متن است.</param>
        /// <returns>یک <see cref="IActionResult"/> که حاوی متن تولید شده است.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        public async Task<IActionResult> Text(AiRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                return Ok(new { responseText = await _generativeAiService.GeminiGenerateTextAsync(request) });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }

        /// <summary>
        /// تصویر تولید می‌کند با استفاده از سرویس Hugging Face.
        /// در صورت وجود ورودی‌های نامعتبر یا بروز خطا، پاسخ مناسب برمی‌گرداند.
        /// </summary>
        /// <param name="request">درخواست AI که شامل اطلاعات مورد نیاز برای تولید تصویر است.</param>
        /// <returns>یک <see cref="IActionResult"/> که حاوی تصویر تولید شده است.</returns>
        /// <exception cref="Exception">در صورت بروز خطای غیرمنتظره پرتاب می‌شود.</exception>
        [HttpPost]
        public async Task<IActionResult> Image(AiRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                return Ok(new { responseImage = await _generativeAiService.HfGenerateImageAsync(request) });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "خطای غیرمنتظره‌ای رخ داده است!", errorDetails = ex.Message });
            }
        }
    }
}