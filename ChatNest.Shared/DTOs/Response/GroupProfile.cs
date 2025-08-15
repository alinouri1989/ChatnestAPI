namespace ChatNest.Shared.DTOs.Response
{
    /// <summary>
    /// Grup profilinin temel bilgilerini ve katılımcı listesini içeren veri transfer nesnesi (DTO).
    /// </summary>
    public sealed record GroupProfile
    {
        public required string Name { get; init; }

        public required string Description { get; init; }

        public required Uri PhotoUrl { get; init; }

        public required Dictionary<string, ParticipantProfile> Participants { get; init; }

        public required DateTime CreatedDate { get; init; }

        public string Id { get; set; } = string.Empty;
        public long CreatedBy { get; set; }
    }
}