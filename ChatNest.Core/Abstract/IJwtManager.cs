namespace ChatNest.Core.Abstract
{
    /// <summary>
    /// رابطی که قرارداد تولید JSON Web Token (JWT) را تعریف می‌کند.
    /// </summary>
    public interface IJwtManager
    {
        /// <summary>
        /// برای شناسه کاربر داده‌شده یک JWT ایجاد می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که برای تولید JWT استفاده می‌شود.</param>
        /// <returns>رشته‌ای که JWT تولیدشده را نمایش می‌دهد.</returns>
        /// <exception cref="ArgumentNullException">در صورت نامعتبر یا ناقص بودن داده‌های پیکربندی پرتاب می‌شود.</exception>
        string GenerateToken(string userId);
    }
}
