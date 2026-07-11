namespace ChatNest.Entities.Models;

public sealed class AppVersionPolicy
{
    public int Id { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Channel { get; set; } = "production";
    public string LatestVersion { get; set; } = string.Empty;
    public int LatestBuild { get; set; }
    public string MinimumSupportedVersion { get; set; } = string.Empty;
    public int MinimumSupportedBuild { get; set; }
    public bool Maintenance { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StoreUrl { get; set; } = string.Empty;
    public int RemindAfterSeconds { get; set; } = 86400;
    public ICollection<AppVersionReleaseNote> ReleaseNotes { get; set; } = [];
}

public sealed class AppVersionReleaseNote
{
    public int Id { get; set; }
    public int AppVersionPolicyId { get; set; }
    public int DisplayOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public AppVersionPolicy AppVersionPolicy { get; set; } = null!;
}
