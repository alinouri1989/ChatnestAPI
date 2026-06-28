namespace ChatNest.Entities.Models;

public class CallParticipant
{
    public Guid CallId { get; set; }
    public string UserId { get; set; }
    public DateTime JoinedAt { get; set; }

    public Call Call { get; set; }
    public User User { get; set; }
}
