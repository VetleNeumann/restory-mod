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
        Assert.All(PatchTargets.All, t => Assert.Contains('.', t.TypeName));
    }
}
