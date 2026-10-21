using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RestoryMod.Core;

namespace RestoryMod;

/// <summary>Runtime lookups for <see cref="PatchTarget"/> entries. Each logs a warning when missing.</summary>
internal static class GameTargets
{
    // AccessTools.TypeByName scans every type in every loaded assembly when the name is not
    // assembly-qualified, which costs about a second per patch class. Ask the game assembly first
    // (nearly every target lives there; nested types as "Outer+Inner"), and remember each type found.
    private static readonly Dictionary<string, Type> Types = new();
    private static readonly Assembly GameAssembly = typeof(Restory.UserInterface.VirtualCursorView).Assembly;

    private static Type? TypeByName(string name)
    {
        if (Types.TryGetValue(name, out var cached)) return cached;
        var type = GameAssembly.GetType(name, throwOnError: false) ?? AccessTools.TypeByName(name);
        if (type is not null) Types[name] = type;
        return type;
    }

    public static MethodInfo? Method(PatchTarget target)
    {
        var type = TypeByName(target.TypeName);
        var method = type is null || target.MemberName is null ? null : AccessTools.Method(type, target.MemberName);
        if (method is null) Plugin.Logger.LogWarning($"Patch target missing: {target}");
        return method;
    }

    /// <summary>
    /// True when each of these targets from the feature's manifest resolves (type, plus member as method,
    /// field or property). Logs each missing one, and an error for any the manifest does not list, since
    /// TargetCheck only covers what manifests list. Meant for a patch class's own Prepare().
    /// </summary>
    public static bool AllPresent(FeatureManifest manifest, params PatchTarget[] targets)
    {
        foreach (var target in manifest.NotListed(targets))
            Plugin.Logger.LogError($"Patch target {target} is not in the {manifest} manifest: add it there so TargetCheck covers it");
        return AllPresent(targets, manifest.Name);
    }

    /// <summary>
    /// True when every target in the feature's manifest resolves; logs each missing one with the
    /// feature's name. Meant for a feature whose patches share one gate.
    /// </summary>
    public static bool AllPresent(FeatureManifest manifest) => AllPresent(manifest.Targets, manifest.Name);

    private static bool AllPresent(IEnumerable<PatchTarget> targets, string feature)
    {
        var all = true;
        foreach (var target in targets)
        {
            var type = TypeByName(target.TypeName);
            if (type is not null && (target.MemberName is null || HasMember(type, target.MemberName))) continue;
            Plugin.Logger.LogWarning($"Patch target missing for {feature}: {target}");
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
        var type = TypeByName(target.TypeName);
        var field = type is null ? null : AccessTools.Field(type, target.MemberName);
        if (field is null) Plugin.Logger.LogWarning($"Patch target missing: {target}");
        return field;
    }
}
