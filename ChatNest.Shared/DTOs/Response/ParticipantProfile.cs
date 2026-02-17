using ChatNest.Entities.Enums;

namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// شیء انتقال داده (DTO) شامل نقش و اطلاعات پایه عضو گروه.
    /// </summary>
    public sealed class ParticipantProfile
    {
        public required string UserId { get; init; }
        public required string DisplayName { get; init; }
        public string? ProfilePhoto { get; init; }   // ❌ required رو بردار
        public GroupParticipant Role { get; init; }
    }
}
