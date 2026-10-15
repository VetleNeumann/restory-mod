using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using RestoryMod.Core;

namespace RestoryMod;

/// <summary>
/// Applies each patch class on its own so one broken target (e.g. after a game update) only
/// disables that feature. Patch classes should implement a static Prepare() that returns false
/// when their targets are missing.
/// </summary>
internal static class FeaturePatcher
{
    public static void Apply(Harmony harmony, ManualLogSource log, params Type[] patchClasses)
    {
        var applied = new List<string>();
        var skipped = new List<string>();

        foreach (var patchClass in patchClasses)
        {
            try
            {
                var patched = harmony.CreateClassProcessor(patchClass).Patch();
                if (patched is { Count: > 0 })
                    applied.Add(patchClass.Name);
                else
                    skipped.Add(patchClass.Name);
            }
            catch (Exception e)
            {
                skipped.Add(patchClass.Name);
                log.LogError($"Patch {patchClass.Name} failed: {e}");
            }
        }

        log.LogInfo($"Patches applied: [{string.Join(", ", applied)}]; skipped: [{string.Join(", ", skipped)}]");
    }
}

/// <summary>Runtime lookups for <see cref="PatchTarget"/> entries. Each logs a warning when missing.</summary>
internal static class GameTargets
{
    public static MethodInfo? Method(PatchTarget target)
    {
        var method = AccessTools.Method(target.TypeName + ":" + target.MemberName);
        if (method is null) Plugin.Logger.LogWarning($"Patch target missing: {target}");
        return method;
    }

    public static FieldInfo? Field(PatchTarget target)
    {
        var type = AccessTools.TypeByName(target.TypeName);
        var field = type is null ? null : AccessTools.Field(type, target.MemberName);
        if (field is null) Plugin.Logger.LogWarning($"Patch target missing: {target}");
        return field;
    }
}
