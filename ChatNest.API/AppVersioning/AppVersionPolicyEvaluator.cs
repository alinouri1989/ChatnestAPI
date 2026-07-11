namespace ChatNest.API.AppVersioning;

using ChatNest.Entities.Models;

public static class AppVersionPolicyEvaluator
{
    public static AppVersionResponse Evaluate(AppVersionPolicy policy, string platform, int currentBuild, string currentVersion)
    {
        var updateType = policy.Maintenance
            ? "maintenance"
            : currentBuild < policy.MinimumSupportedBuild
                ? "required"
                : currentBuild < policy.LatestBuild ? "optional" : "none";

        return new AppVersionResponse(
            platform,
            currentBuild < policy.LatestBuild,
            updateType,
            new AppBuild { Version = currentVersion, Build = currentBuild },
            new AppBuild { Version = policy.LatestVersion, Build = policy.LatestBuild },
            new AppBuild { Version = policy.MinimumSupportedVersion, Build = policy.MinimumSupportedBuild },
            policy.Title,
            policy.Message,
            policy.ReleaseNotes.OrderBy(note => note.DisplayOrder).Select(note => note.Text).ToArray(),
            policy.StoreUrl,
            policy.RemindAfterSeconds);
    }
}
