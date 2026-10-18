using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using DG.Tweening;
using HarmonyLib;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The yellow pulse every workbench screw hint shares (the game's one TooltipProjectionHighlighter):
/// the drop hint, the game's hint button and the mod's additions all add ghosts to it. With
/// [Workbench] TidyHintPulse each screw gets one ghost (the game checks a part once per screw holding
/// it, stacking ghosts), a fading pulse rises again when screws join it so they don't get only its dim
/// tail, and a screw going in or out takes its ghost away instead of leaving it until the pulse ends.
/// </summary>
internal static class HintPulse
{
    public static ConfigEntry<bool> Tidy = null!;

    public static void Bind(ConfigFile config)
    {
        Tidy = config.Bind("Workbench", "TidyHintPulse", true,
            "Keeps the yellow screw hints tidy: one pulse per screw, a fading pulse brightens again when more " +
            "screws join it, and a screw going in or out loses its pulse straight away.");
    }

    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, List<ElementProjection>> Ghosts =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, List<ElementProjection>>("highlightedProjections");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, Sequence?> Sequence =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, Sequence?>("transitionSequence");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, float> ToPeak =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, float>("toPeakDuration");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, float> ToBase =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, float>("toBaseDuration");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, float> PeakScale =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, float>("peakScale");

    /// <summary>The pulse's own members, which every user of this class reads.</summary>
    public static bool TargetsPresent() => GameTargets.AllPresent(
        PatchTargets.HighlighterProjections,
        PatchTargets.HighlighterSequence,
        PatchTargets.HighlighterToPeak,
        PatchTargets.HighlighterToBase,
        PatchTargets.HighlighterPeakScale,
        PatchTargets.TooltipHighlighterCreate);

    /// <summary>
    /// Ghosts asked for so far, counted whether or not one was made: compared before and after running
    /// something, it tells whether that something hinted any screw.
    /// </summary>
    public static int Requests { get; private set; }

    internal static void CountRequest() => Requests++;

    internal static List<ElementProjection> GhostsOf(ProjectionHighlighterBase highlighter) => Ghosts(highlighter);

    /// <summary>The pulse while it plays (or waits paused), else null.</summary>
    public static Sequence? Running(ProjectionHighlighterBase highlighter)
    {
        var pulse = Sequence(highlighter);
        return pulse is not null && pulse.IsActive() ? pulse : null;
    }

    /// <summary>Turns a fading pulse back up from the same brightness.</summary>
    public static void RiseAgain(ProjectionHighlighterBase highlighter)
    {
        var pulse = Running(highlighter);
        if (pulse is null) return;
        var elapsed = pulse.Elapsed(false);
        var position = PulseRewind.Position(elapsed, ToPeak(highlighter), ToBase(highlighter));
        if (position < elapsed) pulse.Goto(position, andPlay: true);
    }

    /// <summary>
    /// Call every frame to keep the pulse at its peak: lets it rise, then waits there. Ghosts that join
    /// meanwhile get the peak size too, which the paused tween would no longer give them.
    /// </summary>
    public static void HoldAtPeak(ProjectionHighlighterBase highlighter)
    {
        var pulse = Running(highlighter);
        if (pulse is null) return;
        var elapsed = pulse.Elapsed(false);
        var toPeak = ToPeak(highlighter);
        var peak = PulseRewind.HoldAtPeak(elapsed, toPeak);
        if (peak is not null) pulse.Goto(peak.Value, andPlay: false);
        else if (elapsed < toPeak) return;

        var scale = Vector3.one * PeakScale(highlighter);
        foreach (var ghost in Ghosts(highlighter))
        {
            if (ghost) ghost.transform.localScale = scale;
        }
    }

    /// <summary>Lets a held pulse fade as usual.</summary>
    public static void Release(ProjectionHighlighterBase highlighter) => Running(highlighter)?.Play();

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Screw hint pulse failed (further errors suppressed): {e}");
    }
}

/// <summary>
/// A screw joining the pulse: counted, the pulse rises again if fading, and a screw that already has a
/// ghost gets no second one.
/// </summary>
[HarmonyPatch]
internal static class HintPulseJoinPatch
{
    private static bool Prepare() => HintPulse.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipHighlighterCreate)!;

    private static bool Prefix(TooltipProjectionHighlighter __instance, Transform parent)
    {
        HintPulse.CountRequest();
        if (!HintPulse.Tidy.Value) return true;
        HintPulse.RiseAgain(__instance);
        foreach (var ghost in HintPulse.GhostsOf(__instance))
        {
            if (ghost && ghost.transform.parent == parent) return false;
        }
        return true;
    }
}

/// <summary>A screw taken out or put in while its slot pulses takes that ghost away.</summary>
[HarmonyPatch]
internal static class HintPulseStaleGhostPatch
{
    private static readonly AccessTools.FieldRef<TooltipProjectionHighlighter, ElementProjectionFactory> Factory =
        AccessTools.FieldRefAccess<TooltipProjectionHighlighter, ElementProjectionFactory>("elementProjectionFactory");

    private static bool Prepare() => HintPulse.TargetsPresent() && GameTargets.AllPresent(
        PatchTargets.TooltipServiceSmallHandler,
        PatchTargets.DropHintHighlighter,
        PatchTargets.TooltipServiceSocketChanged,
        PatchTargets.TooltipHighlighterFactory,
        PatchTargets.ProjectionFactoryDestroySmall);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipServiceSocketChanged)!;

    private static void Postfix(DisassembleTooltipService __instance, ElementSocket socket)
    {
        if (!HintPulse.Tidy.Value || !socket) return;
        try
        {
            var highlighter = TooltipServiceParts.Highlighter(__instance);
            if (!highlighter) return;
            var ghosts = HintPulse.GhostsOf(highlighter!);
            var factory = Factory(highlighter!);
            for (var i = ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = ghosts[i];
                if (!ghost || ghost.transform.parent != socket.transform) continue;
                ghosts.RemoveAt(i);
                factory.DestroySmallElementProjection(ghost);
            }
        }
        catch (Exception e)
        {
            HintPulse.LogOnce(e);
        }
    }
}

/// <summary>The game's tooltip service's way to the pulse. Needs TooltipServiceSmallHandler and DropHintHighlighter.</summary>
internal static class TooltipServiceParts
{
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, SmallElementTooltipHandler> SmallHandler =
        AccessTools.FieldRefAccess<DisassembleTooltipService, SmallElementTooltipHandler>("smallElementTooltipHandler");
    private static readonly AccessTools.FieldRef<SmallElementTooltipHandler, TooltipProjectionHighlighter> HandlerHighlighter =
        AccessTools.FieldRefAccess<SmallElementTooltipHandler, TooltipProjectionHighlighter>("tooltipProjectionHighlighter");

    /// <summary>The highlighter the game's hints pulse with, or null.</summary>
    public static TooltipProjectionHighlighter? Highlighter(DisassembleTooltipService service)
    {
        var handler = SmallHandler(service);
        return handler is null ? null : HandlerHighlighter(handler);
    }
}
