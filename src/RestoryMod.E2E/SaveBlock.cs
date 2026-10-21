using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RestoryMod.Core;

namespace RestoryMod.E2E;

/// <summary>
/// Keeps every save file as it was for the whole end-to-end session: autosaves, manual saves, settings,
/// the profile index, and the game's own clean-ups (deleting old records, moving corrupted files,
/// backups). Scenarios swap the device on the bench and spawn parts, none of which may reach a save.
/// </summary>
internal static class SaveBlock
{
    /// <summary>Patches every write; false when one isn't where it was, so no run may start.</summary>
    public static bool Apply(Harmony harmony)
    {
        var methods = PatchTargets.SaveWrites
            .Select(t => (target: t, found: AccessTools.TypeByName(t.TypeName) is { } type
                ? AccessTools.GetDeclaredMethods(type).Where(m => m.Name == t.MemberName).ToList()
                : new()))
            .ToList();
        if (methods.Any(m => m.found.Count == 0)) return false;
        foreach (var method in methods.SelectMany(m => m.found))
        {
            var skip = method is MethodInfo { ReturnType: var type } && type == typeof(Task) ? nameof(SkipTask) : nameof(Skip);
            harmony.Patch(method, new HarmonyMethod(typeof(SaveBlock), skip));
        }
        return true;
    }

    private static bool Skip() => false;

    private static bool SkipTask(ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }
}
