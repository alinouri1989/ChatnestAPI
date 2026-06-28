using ChatNest.Shared.DTOs.Request;

namespace ChatNest.Services.Abstract
{
    /// <summary>
    /// رابط ارائه‌دهنده سرویس‌های هوش مصنوعی تولیدی.
    /// </summary>
    public interface IGenerativeAiService
    {
        /// <summary>
        /// با استفاده از مدل Gemini متن تولید می‌کند.
        /// </summary>
        /// <param name="request">داده حاوی درخواست ارسالی به مدل هوش مصنوعی.</param>
        /// <returns>متن تولید شده را برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت استفاده از مدل هوش مصنوعی نامعتبر خطا پرتاب می‌شود.</exception>
        Task<string> GeminiGenerateTextAsync(AiRequest request);

        /// <summary>
        /// با استفاده از مدل‌های هوش مصنوعی Hugging Face تصویر تولید می‌کند.
        /// </summary>
        /// <param name="request">داده حاوی درخواست ارسالی به مدل هوش مصنوعی.</param>
        /// <returns>تصویر تولید شده را در فرمت base64 برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">در صورت استفاده از مدل هوش مصنوعی نامعتبر خطا پرتاب می‌شود.</exception>
        Task<string> HfGenerateImageAsync(AiRequest request);
    }
}