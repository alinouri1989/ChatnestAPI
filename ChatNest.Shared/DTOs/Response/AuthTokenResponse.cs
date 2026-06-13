namespace ChatNest.Shared.DTOs.Response;

public sealed class AuthTokenResponse
{
    public required string Token { get; init; }
    public required string RefreshToken { get; init; }
    public DateTime RefreshTokenExpiration { get; init; }
}
