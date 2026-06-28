namespace ChatNest.Services.Exceptions
{
    /// <summary>
    /// خطای اختصاصی که در صورت مسدود بودن دسترسی پرتاب می‌شود.
    /// </summary>
    public sealed class ForbiddenException : Exception
    {
        /// <param name="message">پیام خطا.</param>
        public ForbiddenException(string message) : base(message) { }
    }
}
