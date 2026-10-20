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
        Assert.All(PatchTargets.All, t => Assert.Contains('.', t.TypeName));
    }

    [Fact]
    public void Every_named_target_is_checked_or_listed_as_unchecked()
    {
        var named = typeof(PatchTargets)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(PatchTarget))
            .Select(f => (f.Name, Target: (PatchTarget)f.GetValue(null)!));
        var covered = PatchTargets.All.Concat(PatchTargets.Unchecked).ToHashSet();

        Assert.Empty(named.Where(n => !covered.Contains(n.Target)).Select(n => n.Name));
    }
}
