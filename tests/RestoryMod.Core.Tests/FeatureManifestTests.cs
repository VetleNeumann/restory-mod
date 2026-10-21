using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class FeatureManifestTests
{
    private static readonly PatchTarget Listed = new("Game.Listed", "Member");
    private static readonly PatchTarget Other = new("Game.Listed", "Other");
    private static readonly PatchTarget Elsewhere = new("Game.Elsewhere");

    [Fact]
    public void A_subset_of_the_manifest_has_nothing_unlisted()
    {
        var manifest = new FeatureManifest("Feature", Listed, Other);

        Assert.Empty(manifest.NotListed([Other]));
    }

    [Fact]
    public void Targets_missing_from_the_manifest_are_named()
    {
        var manifest = new FeatureManifest("Feature", Listed);

        Assert.Equal([Other, Elsewhere], manifest.NotListed([Listed, Other, Elsewhere]));
    }
}
