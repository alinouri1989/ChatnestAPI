namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// شیء انتقال داده (DTO) شامل اطلاعات پایه پروفایل گروه و فهرست اعضا.
    /// </summary>
    public sealed class GroupProfile
    {
        public required string Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public DateTime CreatedDate { get; init; }
        public Dictionary<string, ParticipantProfile> Participants { get; set; } = new();
        public string? CreatedBy { get; init; }
        public string? PhotoUrl { get; init; }
    }
}
