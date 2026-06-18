using System.ComponentModel.DataAnnotations;

namespace ChatNest.Shared.DTOs.Request;

public sealed class FirebaseTokenRequest
{
    [Required]
    [MinLength(20)]
    public string Token { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Platform { get; set; }
}
