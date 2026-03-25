using ChatNest.Entities.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ChatNest.Shared.DTOs
{
    public class UserDto
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

        public string DisplayName { get; set; } = string.Empty;

        public string? UserIdentifier { get; set; }

        public string? Biography { get; set; } = string.Empty;

        public Uri? ProfilePhoto { get; set; }
        public string ProviderId { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string BirthDate { get; set; } = string.Empty;
        public DateTime LastConnectionDate { get; set; }

        public string UserSettingsJson { get; set; } = string.Empty;

        [NotMapped]
        public UserSettings UserSettings
        {
            get => string.IsNullOrEmpty(UserSettingsJson) ?
                   new UserSettings() :
                   JsonSerializer.Deserialize<UserSettings>(UserSettingsJson) ?? new UserSettings();
            set => UserSettingsJson = JsonSerializer.Serialize(value);
        }

        public ICollection<GroupDto> CreatedGroups { get; set; } = new List<GroupDto>();
        public virtual ICollection<RefreshTokenDto> RefreshTokens { get; set; }
    }

}
