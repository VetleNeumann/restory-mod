using System.Reflection;
using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class PatchTargetsTests
{
    [Fact]
    public void Targets_are_unique()
    {
        var names = PatchTargets.All.Select(t => t.ToString()).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Targets_use_fully_qualified_type_names()
    {
        Assert.All(FeatureManifests.All.SelectMany(m => m.Targets), t => Assert.Contains('.', t.TypeName));
    }

    [Fact]
    public void Every_named_target_is_in_a_feature_manifest()
    {
        var named = typeof(PatchTargets)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(PatchTarget))
            .Select(f => (f.Name, Target: (PatchTarget)f.GetValue(null)!));
        var owned = FeatureManifests.All.SelectMany(m => m.Targets).ToHashSet();

        Assert.Empty(named.Where(n => !owned.Contains(n.Target)).Select(n => n.Name));
    }

    [Fact]
    public void Unchecked_targets_belong_to_a_feature()
    {
        var owned = FeatureManifests.All.SelectMany(m => m.Targets).ToHashSet();
        Assert.All(PatchTargets.Unchecked, t => Assert.Contains(t, owned));
    }

    [Fact]
    public void All_is_every_manifest_target_except_the_unchecked_ones()
    {
        var expected = FeatureManifests.All.SelectMany(m => m.Targets)
            .Except(PatchTargets.Unchecked)
            .ToHashSet();
        Assert.Equal(expected, PatchTargets.All.ToHashSet());
        Assert.Empty(PatchTargets.All.Intersect(PatchTargets.Unchecked));
    }

    [Fact]
    public void Manifest_names_are_unique()
    {
        var names = FeatureManifests.All.Select(m => m.Name).ToList();
        Assert.NotEmpty(names);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void No_manifest_is_empty()
    {
        Assert.Empty(FeatureManifests.All.Where(m => m.Targets.Count == 0).Select(m => m.Name));
    }

    [Fact]
    public void No_manifest_lists_a_target_twice()
    {
        var repeats = FeatureManifests.All.SelectMany(m => m.Targets
            .GroupBy(t => t)
            .Where(g => g.Count() > 1)
            .Select(g => $"{m.Name}: {g.Key}"));
        Assert.Empty(repeats);
    }
}
