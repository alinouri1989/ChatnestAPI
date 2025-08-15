using ChatNest.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Entities.Models;

public class User : IdentityUser
{
    public string? MobileNo { get; set; }
    public bool MobileConfirmed { get; set; }
    public string? NationalCode { get; set; }
    public DateTime? CreateDate { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public string Fullname
    {
        get
        {
            return Firstname + ' ' + Lastname;
        }
    }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? Biography { get; set; }

    public Uri? ProfilePhoto { get; set; }

    public string ProviderId { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
    public string BirthDate { get; set; }
    public DateTime LastConnectionDate { get; set; }

    // Store as JSON string in database
    public string UserSettingsJson { get; set; } = string.Empty;

    [NotMapped]
    public UserSettings UserSettings
    {
        get => string.IsNullOrEmpty(UserSettingsJson) ?
               new UserSettings() :
               JsonSerializer.Deserialize<UserSettings>(UserSettingsJson) ?? new UserSettings();
        set => UserSettingsJson = JsonSerializer.Serialize(value);
    }

    // Navigation properties
    public ICollection<Group> CreatedGroups { get; set; } = new List<Group>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; }

}
