using ChatNest.API.AppVersioning;
using ChatNest.Entities.Models;
using Xunit;

namespace ChatNest.Tests;

public sealed class AppVersionPolicyEvaluatorTests
{
    private static readonly AppVersionPolicy Policy = new()
    {
        Platform = "android",
        LatestVersion = "2.5.0",
        LatestBuild = 130,
        MinimumSupportedVersion = "2.3.0",
        MinimumSupportedBuild = 110
    };

    [Theory]
    [InlineData(130, "none", false)]
    [InlineData(125, "optional", true)]
    [InlineData(109, "required", true)]
    public void Evaluate_UsesIntegerBuilds(int build, string expectedType, bool updateAvailable)
    {
        var result = AppVersionPolicyEvaluator.Evaluate(Policy, "android", build, "99.0.0");

        Assert.Equal(expectedType, result.UpdateType);
        Assert.Equal(updateAvailable, result.UpdateAvailable);
        Assert.Equal(build, result.Current.Build);
        Assert.Equal("99.0.0", result.Current.Version);
    }

    [Fact]
    public void Evaluate_MaintenanceOverridesBuildPolicy()
    {
        var policy = new AppVersionPolicy
        {
            Maintenance = true,
            LatestVersion = "2.5.0",
            LatestBuild = 130,
            MinimumSupportedVersion = "2.3.0",
            MinimumSupportedBuild = 110
        };

        var result = AppVersionPolicyEvaluator.Evaluate(policy, "pwa", 130, "2.5.0");

        Assert.Equal("maintenance", result.UpdateType);
        Assert.False(result.UpdateAvailable);
    }
}
