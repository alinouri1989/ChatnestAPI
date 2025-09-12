namespace ChatNest.Entities.Models;

public class ChatParticipant
{
    public Guid ChatId { get; set; }
    public string UserId { get; set; }
    public DateTime JoinedAt { get; set; }

    public Chat Chat { get; set; }
    public User User { get; set; }
}
