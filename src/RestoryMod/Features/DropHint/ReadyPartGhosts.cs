using System;
using System.Collections.Generic;
using System.Reflection;
using EPOOutline;
using HarmonyLib;
using Restory.Data.Outline;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The every-part hint's ghosts while the pulse runs: each one leaves as soon as its part stops being
/// ready (picked up, put in, its socket filled or blocked), each is filled in rather than only
/// outlined, and ghosts in missing sockets pulse red instead of yellow. They share the game's pulse,
/// so it still sets their timing.
/// </summary>
internal static class ReadyPartGhosts
{
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, OutlineSettingsPreset> Preset =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, OutlineSettingsPreset>("outlinePresetInstance");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, Outlinable.OutlineProperties?> Front =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, Outlinable.OutlineProperties?>("frontOutlineProperties");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, Outlinable.OutlineProperties?> Back =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, Outlinable.OutlineProperties?>("backOutlineProperties");

    private sealed class Tracked
    {
        public Tracked(TooltipProjectionHighlighter highlighter, ElementProjection ghost, Func<bool> stillReady, bool missing,
            Func<bool>? waiting, bool restyle, ElementProjection? muted)
        {
            Restyle = restyle;
            Muted = muted;
            Highlighter = highlighter;
            Ghost = ghost;
            StillReady = stillReady;
            Missing = missing;
            Waiting = waiting;
        }

        public TooltipProjectionHighlighter Highlighter { get; }
        public ElementProjection Ghost { get; }
        public Func<bool> StillReady { get; }
        public bool Missing { get; }

        /// <summary>
        /// False for a ghost that keeps the pulse's own look: screw slots, where the game draws its own
        /// marker on an empty one and a restyled ghost ends up hidden under it, and screws still in, to match.
        /// </summary>
        public bool Restyle { get; }

        /// <summary>
        /// The game's own marker on the same spot, its outline switched off while this ghost shows: the
        /// two would draw in no set order, so the marker could hide the ghost.
        /// </summary>
        public ElementProjection? Muted { get; }

        /// <summary>True while something else should go first: the ghost shows faint and doesn't pulse.</summary>
        public Func<bool>? Waiting { get; }

        /// <summary>The preset the ghost wears now, or null before it gets one of ours.</summary>
        public OutlineSettingsPreset? Wearing { get; set; }
    }

    private static readonly List<Tracked> Ghosts = new();

    /// <summary>The flat see-through fill the game's own part ghosts use; null if the build lacks it.</summary>
    private static readonly Shader? FillShader = Shader.Find("Hidden/EPO/Fill/Basic/Color fill");

    /// <summary>
    /// Copies of the pulse's outline preset for this hint's ghosts: yellow, red, and a faint copy of
    /// each for ghosts that wait their turn. They add a fill, so a ghost shows the whole part rather
    /// than an outline that merges with its neighbours' (overlapping fills add up and look darker).
    /// Each look needs its own copy because a preset's colors are shared by every ghost wearing it.
    /// </summary>
    private static readonly OutlineSettingsPreset?[] Presets = new OutlineSettingsPreset?[4];

    /// <summary>
    /// Adds a ghost to the pulse that stays only while <paramref name="stillReady"/> holds, and shows
    /// faint while <paramref name="waiting"/> does.
    /// </summary>
    public static void Add(TooltipProjectionHighlighter highlighter, ElementProjectionData data, Transform parent,
        Func<bool> stillReady, bool missing = false, Func<bool>? waiting = null, bool restyle = true,
        ElementProjection? covering = null)
    {
        var ghost = HintPulse.Add(highlighter, data, parent);
        if (!ghost) return;
        var wearing = Ghosts.Find(t => t.Ghost == ghost)?.Wearing;
        Ghosts.RemoveAll(t => t.Ghost == ghost);
        var tracked = new Tracked(highlighter, ghost!, stillReady, missing, waiting, restyle, covering) { Wearing = wearing };
        if (covering) covering!.OutlinableAdapter.Outlinable.enabled = false;
        Ghosts.Add(tracked);
        Tint(tracked);
    }

    /// <summary>Every frame: drops ghosts whose part is no longer ready and keeps the rest in step with the pulse.</summary>
    public static void Tick()
    {
        for (var i = Ghosts.Count - 1; i >= 0; i--)
        {
            var tracked = Ghosts[i];
            bool keep;
            try
            {
                keep = tracked.Ghost && HintPulse.GhostsOf(tracked.Highlighter).Contains(tracked.Ghost) && tracked.StillReady();
                if (keep) Tint(tracked);
            }
            catch (Exception)
            {
                keep = false; // its part, socket or ghost was destroyed with the device
            }
            if (keep) continue;
            Ghosts.RemoveAt(i);
            Restore(tracked);
            if (tracked.Ghost && tracked.Highlighter) HintPulse.Remove(tracked.Highlighter, tracked.Ghost);
        }
    }

    /// <summary>The pulse ended and is about to pool its ghosts: give them the pulse's own preset back first.</summary>
    public static void PulseEnding(TooltipProjectionHighlighter highlighter)
    {
        for (var i = Ghosts.Count - 1; i >= 0; i--)
        {
            if (Ghosts[i].Highlighter != highlighter) continue;
            Restore(Ghosts[i]);
            Ghosts.RemoveAt(i);
        }
    }

    /// <summary>On unload: hands every ghost back to the plain pulse and drops the copied presets.</summary>
    internal static void Unload()
    {
        foreach (var tracked in Ghosts) Restore(tracked);
        Ghosts.Clear();
        for (var i = 0; i < Presets.Length; i++)
        {
            if (Presets[i]) UnityEngine.Object.Destroy(Presets[i]);
            Presets[i] = null;
        }
    }

    private static OutlineSettingsPreset Copy(ProjectionHighlighterBase highlighter, bool missing, bool waiting)
    {
        var i = (missing ? 2 : 0) + (waiting ? 1 : 0);
        if (!Presets[i]) Presets[i] = UnityEngine.Object.Instantiate(Preset(highlighter));
        return Presets[i]!;
    }

    private static void Restore(Tracked tracked)
    {
        // Even a marker the game has put back in its pool: the next one made from it must draw.
        if (tracked.Muted) tracked.Muted!.OutlinableAdapter.Outlinable.enabled = true;
        if (tracked.Restyle && tracked.Ghost && tracked.Highlighter)
            tracked.Ghost.OutlinableAdapter.OverridePreset = Preset(tracked.Highlighter);
    }

    /// <summary>
    /// Gives a ghost the pulse's current brightness, in yellow or red, on its outline and fill; a
    /// waiting ghost gets a faint steady look instead.
    /// </summary>
    private static void Tint(Tracked tracked)
    {
        if (!tracked.Restyle) return;
        var front = Front(tracked.Highlighter);
        if (front is null) return;
        var waiting = tracked.Waiting?.Invoke() ?? false;
        var preset = Copy(tracked.Highlighter, tracked.Missing, waiting);
        if (tracked.Wearing != preset)
        {
            tracked.Ghost.OutlinableAdapter.OverridePreset = preset;
            tracked.Wearing = preset;
        }
        var outline = tracked.Ghost.OutlinableAdapter.Outlinable;
        if (outline.RenderStyle == RenderStyle.Single)
        {
            Paint(outline.OutlineParameters, front.Color, tracked.Missing, waiting);
            return;
        }
        Paint(outline.FrontParameters, front.Color, tracked.Missing, waiting);
        Paint(outline.BackParameters, (Back(tracked.Highlighter) ?? front).Color, tracked.Missing, waiting);
    }

    private static void Paint(Outlinable.OutlineProperties properties, Color pulse, bool missing, bool waiting)
    {
        // Waiting: the pulse's peak color held still and darkened (the outline ignores alpha).
        if (waiting) pulse = new Color(0.45f, 0.45f, 0f, pulse.a);
        var color = missing ? Red(pulse) : pulse;
        properties.Color = color;
        if (!FillShader) return;
        if (properties.FillPass.Shader != FillShader) properties.FillPass.Shader = FillShader;
        // The pulse runs white (blue 1) to yellow (blue 0): the fill thickens as it brightens.
        var alpha = waiting ? 0.08f : Mathf.Lerp(0.15f, 0.35f, 1f - pulse.b);
        properties.FillPass.SetColor("_PublicColor", new Color(color.r, color.g, color.b, alpha));
    }

    // The pulse runs white to yellow; this keeps its brightness and turns it red.
    private static Color Red(Color pulse) => new(pulse.r, pulse.g * 0.2f, pulse.b * 0.2f, pulse.a);
}

/// <summary>Runs the every-part hint's per-frame check.</summary>
internal sealed class ReadyPartGhostWatcher : MonoBehaviour
{
    private static bool errorLogged;

    private void Update()
    {
        try
        {
            ReadyPartGhosts.Tick();
        }
        catch (Exception e)
        {
            if (errorLogged) return;
            errorLogged = true;
            Plugin.Logger.LogError($"Every-part hint ghosts failed (further errors suppressed): {e}");
        }
    }
}

/// <summary>The pulse ending: red ghosts turn back to yellow before the game pools them.</summary>
[HarmonyPatch]
internal static class ReadyPartPulseEndPatch
{
    private static bool Prepare() => EveryPartHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipHighlighterComplete)!;

    private static void Prefix(TooltipProjectionHighlighter __instance) => ReadyPartGhosts.PulseEnding(__instance);
}
