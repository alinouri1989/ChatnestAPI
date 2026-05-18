namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// شیء انتقال داده (DTO) شامل اطلاعات پایه کاربر.
    /// </summary>
    public sealed record CallerUser
    {
        public required string DisplayName { get; init; }

        public required Uri ProfilePhoto { get; init; }

        public required DateTime LastConnectionDate { get; set; }

        public bool IsOnline { get; set; }
    }
}
