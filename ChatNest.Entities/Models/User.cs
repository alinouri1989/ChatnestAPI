using ChatNest.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Entities.Models;

public class User : IdentityUser
{
    public string? MobileNo { get; set; } = string.Empty;
    public bool MobileConfirmed { get; set; }
    public string? NationalCode { get; set; } = string.Empty;
    public DateTime? CreateDate { get; set; }
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public string Fullname
    {
        get
        {
            return Firstname + ' ' + Lastname;
        }
    }

    // ❌ Remove this - IdentityUser already has Email property
    // [Required, EmailAddress]
    // public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? UserIdentifier { get; set; }

    // ❌ Remove this too - IdentityUser already has PhoneNumber
    // [Phone]
    // public string? PhoneNumber { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Biography { get; set; } = string.Empty;

    public Uri? ProfilePhoto { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string BirthDate { get; set; } = string.Empty;
    public DateTime LastConnectionDate { get; set; }

    // Store as JSON string in database
    public string UserSettingsJson { get; set; } = string.Empty;

    public string FcmTokensJson { get; set; } = string.Empty;

    [NotMapped]
    public UserSettings UserSettings
    {
        get => string.IsNullOrEmpty(UserSettingsJson) ?
               new UserSettings() :
               JsonSerializer.Deserialize<UserSettings>(UserSettingsJson) ?? new UserSettings();
        set => UserSettingsJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public List<string> FcmTokens
    {
        get => string.IsNullOrEmpty(FcmTokensJson) ?
               new List<string>() :
               JsonSerializer.Deserialize<List<string>>(FcmTokensJson) ?? new List<string>();
        set => FcmTokensJson = JsonSerializer.Serialize(value.Distinct().ToList());
    }

    // Navigation properties
    public ICollection<Group> CreatedGroups { get; set; } = new List<Group>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
