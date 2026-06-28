using ChatNest.Entities.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatNest.Entities.Identity
{
    public class RefreshToken
    {
        [Key]
        public string Token { get; set; } = string.Empty;

        public DateTime Expiration { get; set; }

        public bool IsExpired => DateTime.UtcNow >= Expiration;

        public DateTime Created { get; set; }

        public DateTime? Revoked { get; set; }

        public bool IsActive { get; set; }

        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;

        public virtual User User { get; set; } = null!;
    }
}
