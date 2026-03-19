namespace ChatNest.Shared.DTOs
{
    public class ChatParticipantDto
    {
        public Guid ChatId { get; set; }
        public string UserId { get; set; }
        public DateTime JoinedAt { get; set; }

        public ChatDto Chat { get; set; }
        public UserDto User { get; set; }
    }
}
