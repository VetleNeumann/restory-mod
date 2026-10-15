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

    /// <summary>
    /// True when every target resolves (type, plus member as method, field or property).
    /// Logs each missing one; meant for patch classes' Prepare().
    /// </summary>
    public static bool AllPresent(params PatchTarget[] targets)
    {
        var all = true;
        foreach (var target in targets)
        {
            var type = AccessTools.TypeByName(target.TypeName);
            if (type is not null && (target.MemberName is null || HasMember(type, target.MemberName))) continue;
            Plugin.Logger.LogWarning($"Patch target missing: {target}");
            all = false;
        }
        return all;
    }

    /// <summary>
    /// Any member with this name on the type or its base types. Plain reflection rather than
    /// AccessTools.Method/Field/Property, which log a warning for every kind that doesn't match.
    /// </summary>
    private static bool HasMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.GetMember(name, flags).Length > 0) return true;
        }
        return false;
    }

    public static FieldInfo? Field(PatchTarget target)
    {
        var type = AccessTools.TypeByName(target.TypeName);
        var field = type is null ? null : AccessTools.Field(type, target.MemberName);
        if (field is null) Plugin.Logger.LogWarning($"Patch target missing: {target}");
        return field;
    }
}
