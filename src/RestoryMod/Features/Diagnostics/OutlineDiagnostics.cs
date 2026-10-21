using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using EPOOutline;
using HarmonyLib;
using Restory.Data.Outline;
using Restory.Gameplay.Common;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.Diagnostics;

internal sealed class OutlineDiagnosticsFeature : IFeature
{
    public FeatureManifest Manifest => FeatureManifests.OutlineDiagnostics;

    public void Load(FeatureContext ctx)
    {
        OutlineDiagnostics.Bind(ctx.Config);
        ctx.Patch(typeof(OutlineAdapterActivePatch), typeof(OutlineAdapterPresetPatch), typeof(ProjectionPulsePatch),
            typeof(DropHintGraphPatch));
        if (OutlineDiagnostics.Enabled) ctx.AddPoller<OutlineWatcher>();
    }
}

/// <summary>
/// Dev aid: logs every outline change on device parts, screws and the slots they go back into, to
/// find out what lights a screw and what turns it off again. The game switches outlines through
/// <see cref="OutlinableAdapter"/> (logged with the calling code); <see cref="OutlineWatcher"/>
/// catches everything else, such as colour tweens and outlines switched outside the adapter.
/// Toggle with [Debug] OutlineDiagnostics (needs a restart).
/// </summary>
internal static class OutlineDiagnostics
{
    private static ConfigEntry<bool> entry = null!;
    private static ConfigEntry<bool> dump = null!;

    public static bool Enabled => entry.Value;

    /// <summary>Set by [Debug] DumpOutlines; the watcher logs every outline once and clears it.</summary>
    public static bool DumpRequested;

    internal static void Bind(ConfigFile config)
    {
        entry = config.Bind("Debug", "OutlineDiagnostics", false,
            "Log every outline change on device parts, screws and their slots: what switched it, the preset, " +
            "and colour changes frame by frame. Needs a game restart to switch on or off.");
        dump = config.Bind("Debug", "DumpOutlines", false,
            "Set to true to log every outline on screen once, with its object path (needs OutlineDiagnostics on). " +
            "Turns itself off again.");
        // Without the watcher nothing would ever answer the request, so it clears at once.
        if (!entry.Value) dump.Value = false;
        dump.SettingChanged += (_, _) =>
        {
            if (!dump.Value) return;
            if (entry.Value) DumpRequested = true;
            else
            {
                Log("dump: needs [Debug] OutlineDiagnostics on at startup");
                dump.Value = false;
            }
        };
    }

    /// <summary>Logs every outline drawn now, whatever it belongs to, then clears the request.</summary>
    public static void Dump()
    {
        DumpRequested = false;
        dump.Value = false;
        var all = Object.FindObjectsByType<Outlinable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Log($"dump: {all.Count(o => o.enabled)} outlines on");
        foreach (var outline in all)
        {
            if (!outline.enabled) continue;
            var adapter = outline.GetComponent<OutlinableAdapter>();
            var preset = adapter ? PresetName(adapter.OverridePreset) : "(no adapter)";
            Log($"dump: {PathOf(outline.transform)} preset={preset}: {Look(outline)}");
        }
        foreach (var name in FillShaders) LogProperties(name);
    }

    // The patterned fill the missing look wears.
    private static readonly string[] FillShaders = { "Hidden/EPO/Fill/Basic/Dots" };

    private static void LogProperties(string name)
    {
        var shader = Shader.Find(name);
        if (!shader)
        {
            Log($"dump: shader {name} not in the build");
            return;
        }
        var props = Enumerable.Range(0, shader.GetPropertyCount())
            .Select(i => $"{shader.GetPropertyName(i)}:{shader.GetPropertyType(i)}" +
                         (shader.GetPropertyType(i) is UnityEngine.Rendering.ShaderPropertyType.Float or UnityEngine.Rendering.ShaderPropertyType.Range
                             ? $"={shader.GetPropertyDefaultFloatValue(i)}" : ""));
        Log($"dump: shader {name}: {string.Join(", ", props)}");
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var at = t; at; at = at.parent) parts.Add(at.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    public static void Log(string message) =>
        Plugin.Logger.LogInfo($"[Outline f{Time.frameCount} {Time.unscaledTime:F2}s] {message}");

    /// <summary>What the outline belongs to, with the element's state; null for anything else.</summary>
    public static string? Describe(Component outline)
    {
        // Slots sit inside the part that holds them, so look for a projection first.
        var projection = outline.GetComponentInParent<ElementProjection>(true);
        if (projection) return $"slot '{projection.name}'";

        var element = outline.GetComponentInParent<ElementBase>(true);
        if (!element) return null;
        return $"{element.GetType().Name} '{element.name}' (blocked={element.IsBlocked}, selected={element.IsSelected}, " +
               $"highlighted={element.IsHighlighted}, activated={element.IsActivated}, inSocket={element.InSocket})";
    }

    public static string PresetName(OutlineSettingsPreset? preset) => preset ? preset!.name : "(base preset)";

    /// <summary>The game methods that led here, nearest first.</summary>
    public static string Callers()
    {
        var names = new StackTrace(1, false).GetFrames()
            .Select(f => f.GetMethod())
            .Where(m => m != null && m.DeclaringType != null && m.DeclaringType.Namespace?.StartsWith("Restory.") == true)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Take(4);
        return "via " + string.Join(" < ", names);
    }

    /// <summary>Everything about an outline that changes how it looks.</summary>
    public static string Look(Outlinable outline)
    {
        var sb = new StringBuilder(outline.enabled ? "on" : "off");
        sb.Append($" {outline.RenderStyle} mode={outline.DrawingMode} layer={outline.OutlineLayer}");
        if (outline.RenderStyle == RenderStyle.Single)
        {
            Append(sb, "", outline.OutlineParameters);
        }
        else
        {
            Append(sb, "front", outline.FrontParameters);
            Append(sb, "back", outline.BackParameters);
        }
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string label, Outlinable.OutlineProperties? p)
    {
        sb.Append(' ').Append(label).Append(label.Length > 0 ? "=" : "");
        if (p is null || !p.Enabled)
        {
            sb.Append("hidden");
            return;
        }
        sb.Append('#').Append(ColorUtility.ToHtmlStringRGBA(p.Color));
        var fill = p.FillPass?.Shader;
        if (fill) sb.Append(" fill=").Append(fill!.name);
    }
}

[HarmonyPatch]
internal static class OutlineAdapterActivePatch
{
    private static bool Prepare() =>
        OutlineDiagnostics.Enabled && GameTargets.AllPresent(FeatureManifests.OutlineDiagnostics, PatchTargets.OutlineAdapterSetActive);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.OutlineAdapterSetActive)!;

    private static void Prefix(OutlinableAdapter __instance, bool value)
    {
        if (__instance.IsActive == value) return;
        var what = OutlineDiagnostics.Describe(__instance);
        if (what is null) return;
        OutlineDiagnostics.Log(
            $"{(value ? "ON " : "OFF")} {what} preset={OutlineDiagnostics.PresetName(__instance.OverridePreset)} {OutlineDiagnostics.Callers()}");
    }
}

[HarmonyPatch]
internal static class OutlineAdapterPresetPatch
{
    private static bool Prepare() =>
        OutlineDiagnostics.Enabled && GameTargets.AllPresent(FeatureManifests.OutlineDiagnostics, PatchTargets.OutlineAdapterSetPreset);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.OutlineAdapterSetPreset)!;

    private static void Prefix(OutlinableAdapter __instance, OutlineSettingsPreset value)
    {
        if (__instance.OverridePreset == value) return;
        var what = OutlineDiagnostics.Describe(__instance);
        if (what is null) return;
        OutlineDiagnostics.Log(
            $"preset {OutlineDiagnostics.PresetName(__instance.OverridePreset)} -> {OutlineDiagnostics.PresetName(value)} " +
            $"on {what} {OutlineDiagnostics.Callers()}");
    }
}

[HarmonyPatch]
internal static class ProjectionPulsePatch
{
    private static bool Prepare() =>
        OutlineDiagnostics.Enabled && GameTargets.AllPresent(FeatureManifests.OutlineDiagnostics, PatchTargets.ProjectionHighlighterPlay);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ProjectionHighlighterPlay)!;

    private static void Prefix(MonoBehaviour __instance) =>
        OutlineDiagnostics.Log($"slot pulse starts ({__instance.GetType().Name}) {OutlineDiagnostics.Callers()}");
}

/// <summary>
/// Each frame, compares how every part, screw and slot outline looks with the frame before, and logs
/// the difference. Idle unless [Debug] OutlineDiagnostics was on at startup.
/// </summary>
internal sealed class OutlineWatcher : MonoBehaviour
{
    private readonly Dictionary<int, string> looks = new();
    private readonly HashSet<int> seen = new();
    private readonly List<int> gone = new();

    private void LateUpdate()
    {
        if (OutlineDiagnostics.DumpRequested) OutlineDiagnostics.Dump();
        seen.Clear();
        foreach (var outline in FindObjectsByType<Outlinable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var id = outline.GetInstanceID();
            seen.Add(id);

            var look = OutlineDiagnostics.Look(outline);
            if (looks.TryGetValue(id, out var before) && before == look) continue;
            looks[id] = look;

            // Checked on every change, not once: pooled objects move under parts and slots later.
            var what = OutlineDiagnostics.Describe(outline);
            if (what is null) continue;
            OutlineDiagnostics.Log(before is null ? $"seen {what}: {look}" : $"look {what}: {before} -> {look}");
        }

        gone.Clear();
        foreach (var id in looks.Keys)
        {
            if (!seen.Contains(id)) gone.Add(id);
        }
        foreach (var id in gone) looks.Remove(id);
    }
}

/// <summary>
/// Logs the socket graph the game's drop hint reads: which parts the removed one blocked, and what
/// still holds each of them. Only parts held by unblocked screws alone get their screws pulsed.
/// </summary>
[HarmonyPatch]
internal static class DropHintGraphPatch
{
    private static FieldInfo? additionalBlockers;

    private static bool Prepare()
    {
        if (!OutlineDiagnostics.Enabled || !GameTargets.AllPresent(FeatureManifests.OutlineDiagnostics,
                PatchTargets.DropHintHandleBlocked, PatchTargets.DropHintDetachedSocket, PatchTargets.SocketAdditionalBlockers))
            return false;
        additionalBlockers = GameTargets.Field(PatchTargets.SocketAdditionalBlockers);
        return true;
    }

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DropHintHandleBlocked)!;

    private static void Prefix(ElementSocket? ___detachedSocket)
    {
        if (!___detachedSocket)
        {
            OutlineDiagnostics.Log("drop hint: no detached socket, nothing to pulse");
            return;
        }
        var sb = new StringBuilder($"drop hint: removed from {Socket(___detachedSocket!)}; it blocked:");
        foreach (var blocked in ___detachedSocket!.BlockedSockets)
        {
            sb.Append("\n  ").Append(Socket(blocked)).Append(", held by:");
            foreach (var blocker in blocked.Blockers)
                sb.Append("\n    ").Append(Socket(blocker));
            foreach (var blocker in AdditionalBlockers(blocked))
                sb.Append("\n    (additional, ignored by the hint) ").Append(Socket(blocker));
            if (blocked.CompatibleElementInfo.Category != Restory.Data.Elements.ElementCategory.Small) continue;
            foreach (var next in blocked.BlockedSockets)
            {
                sb.Append("\n    which blocks ").Append(Socket(next)).Append(", held by:");
                foreach (var blocker in next.Blockers)
                    sb.Append("\n      ").Append(Socket(blocker));
            }
        }
        OutlineDiagnostics.Log(sb.ToString());
    }

    private static IEnumerable<ElementSocket> AdditionalBlockers(ElementSocket socket) =>
        additionalBlockers?.GetValue(socket) as IEnumerable<ElementSocket>
        ?? Enumerable.Empty<ElementSocket>();

    private static string Socket(ElementSocket socket)
    {
        var nested = socket.NestedElement;
        var holds = nested ? $"'{nested.name}'" : "empty";
        return $"socket '{socket.name}' [{socket.CompatibleElementInfo.Category}, {holds}, blocked={socket.IsBlocked}]";
    }
}
