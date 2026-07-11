namespace ChatNest.API.AppVersioning;

public sealed class AppBuild
{
    public string Version { get; init; } = string.Empty;
    public int Build { get; init; }
}

public sealed record AppVersionResponse(
    string Platform,
    bool UpdateAvailable,
    string UpdateType,
    AppBuild Current,
    AppBuild Latest,
    AppBuild MinimumSupported,
    string Title,
    string Message,
    IReadOnlyList<string> ReleaseNotes,
    string StoreUrl,
    int RemindAfterSeconds);
