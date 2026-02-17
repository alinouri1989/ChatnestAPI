namespace ChatNest.Services.Exceptions
{
    /// <summary>
    /// خطای اختصاصی که در صورت یافت نشدن منبع پرتاب می‌شود.
    /// </summary>
    public sealed class NotFoundException : Exception
    {
        /// <param name="message">پیام خطا.</param>
        public NotFoundException(string message) : base(message) { }
    }
}
