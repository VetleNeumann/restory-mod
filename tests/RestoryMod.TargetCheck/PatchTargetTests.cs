using System.Reflection;
using RestoryMod.Core;

namespace RestoryMod.TargetCheck;

/// <summary>
/// Resolves every <see cref="PatchTargets"/> entry against the installed game's assembly
/// (metadata only, nothing is executed). Run after each game update: a failure here means a
/// patch will be skipped at runtime.
/// </summary>
public class PatchTargetTests
{
    private static readonly Lazy<(MetadataLoadContext Context, Assembly Game)> Game = new(LoadGame);

    public static TheoryData<string> Targets()
    {
        var data = new TheoryData<string>();
        foreach (var target in PatchTargets.All)
            data.Add(target.ToString());
        return data;
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Target_resolves_in_installed_game(string target)
    {
        var entry = PatchTargets.All.Single(t => t.ToString() == target);
        var type = Game.Value.Game.GetType(entry.TypeName, throwOnError: false);
        Assert.True(type is not null, $"Type '{entry.TypeName}' not found in {PatchTargets.GameAssembly}.");

        if (entry.MemberName is not null)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            Assert.True(type!.GetMember(entry.MemberName, all).Length > 0,
                $"Member '{entry.MemberName}' not found on '{entry.TypeName}'.");
        }
    }

    private static (MetadataLoadContext, Assembly) LoadGame()
    {
        var managed = typeof(PatchTargetTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GameManaged")?.Value;

        if (string.IsNullOrEmpty(managed) || !Directory.Exists(managed))
            throw new InvalidOperationException(
                "Game install not found. Copy GamePaths.props.example to GamePaths.props and set GameDir.");

        var resolver = new PathAssemblyResolver(Directory.GetFiles(managed, "*.dll"));
        var context = new MetadataLoadContext(resolver, coreAssemblyName: "mscorlib");
        var game = context.LoadFromAssemblyPath(Path.Combine(managed, PatchTargets.GameAssembly + ".dll"));
        return (context, game);
    }
}
