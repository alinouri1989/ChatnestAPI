using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
