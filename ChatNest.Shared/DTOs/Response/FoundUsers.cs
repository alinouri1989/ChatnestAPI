namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// شیء انتقال داده (DTO) شامل اطلاعات پایه کاربران یافت‌شده در نتیجه جست‌وجو.
    /// </summary>
    public sealed record FoundUsers
    {
        public required string DisplayName { get; init; }

        public required string Email { get; init; }

        public required Uri ProfilePhoto { get; init; }
    }
}
