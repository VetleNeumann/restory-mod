using System.Reflection;
using RestoryMod.Core;

namespace RestoryMod.TargetCheck;

/// <summary>
/// Loads the installed game's assembly as metadata only (nothing is executed). Shared by all
/// target checks and disposed after them. Null when no game install is configured, e.g. in CI.
/// </summary>
public sealed class InstalledGame : IDisposable
{
    private readonly MetadataLoadContext? context;

    public InstalledGame()
    {
        var managed = typeof(InstalledGame).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GameManaged")?.Value;
        if (string.IsNullOrEmpty(managed) || !Directory.Exists(managed)) return;

        var resolver = new PathAssemblyResolver(Directory.GetFiles(managed, "*.dll"));
        context = new MetadataLoadContext(resolver, coreAssemblyName: "mscorlib");
        Assembly = context.LoadFromAssemblyPath(Path.Combine(managed, PatchTargets.GameAssembly + ".dll"));
    }

    public Assembly? Assembly { get; }

    public void Dispose() => context?.Dispose();
}

/// <summary>
/// Resolves every <see cref="PatchTargets"/> entry against the installed game. Run after each game
/// update: a failure here means a patch will be skipped at runtime. Skips when the game isn't
/// configured (no GamePaths.props), so CI stays green.
/// </summary>
public class PatchTargetTests(InstalledGame game) : IClassFixture<InstalledGame>
{
    public static TheoryData<string> Targets()
    {
        var data = new TheoryData<string>();
        foreach (var target in PatchTargets.All)
            data.Add(target.ToString());
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(Targets))]
    public void Target_resolves_in_installed_game(string target)
    {
        Skip.If(game.Assembly is null,
            "Game install not configured: copy GamePaths.props.example to GamePaths.props and set GameDir.");

        var entry = PatchTargets.All.Single(t => t.ToString() == target);
        var type = game.Assembly!.GetType(entry.TypeName, throwOnError: false);
        Assert.True(type is not null, $"Type '{entry.TypeName}' not found in {PatchTargets.GameAssembly}.");

        if (entry.MemberName is not null)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            Assert.True(type!.GetMember(entry.MemberName, all).Length > 0,
                $"Member '{entry.MemberName}' not found on '{entry.TypeName}'.");
        }
    }
}
