namespace ChatNest.Shared.DTOs
{
    public class RefreshTokenDto
    {
        public string Token { get; set; }

        public DateTime Expiration { get; set; }

        public bool IsExpired => DateTime.UtcNow >= Expiration;

        public DateTime Created { get; set; }

        public DateTime? Revoked { get; set; }

        public bool IsActive { get; set; }

        public string UserId { get; set; }

        public virtual UserDto User { get; set; }
    }

}
