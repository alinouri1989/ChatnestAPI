using ChatNest.Entities.Enums;

namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// شیء انتقال داده (DTO) شامل نقش و اطلاعات پایه عضو گروه.
    /// </summary>
    public sealed record ParticipantProfile
    {
        public required string DisplayName { get; init; }

        public required GroupParticipant Role { get; init; }

        public required Uri ProfilePhoto { get; init; }

        public DateTime? LastConnectionDate { get; set; }
    }
}
