namespace ChatNest.Services.Exceptions
{
    /// <summary>
    /// خطای اختصاصی که در صورت درخواست نامعتبر پرتاب می‌شود.
    /// </summary>
    public sealed class BadRequestException : Exception
    {
        /// <param name="message">پیام خطا.</param>
        public BadRequestException(string message) : base(message) { }
    }
}
