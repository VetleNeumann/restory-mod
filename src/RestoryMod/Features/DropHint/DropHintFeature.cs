using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using DG.Tweening;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The drop hint: when a removed part is dropped, the game pulses yellow ghosts over the screws that
/// are now the last thing holding some part. The mod also ghosts every screw the dropped part freed,
/// and gives each screw one ghost (the game checks a part once per screw holding it, stacking ghosts).
/// Screws joining a pulse that is already fading (a second drop soon after the first) would only get
/// its dim tail, so the pulse turns round and rises again.
/// </summary>
internal static class DropHintFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Workbench", "DropHintEveryFreedScrew", true,
            "When you drop a part you removed, the yellow pulse also covers every screw that part freed, " +
            "not only screws that are the last thing holding another part. Each screw gets one pulse.");
    }

    public static bool TargetsPresent() => GameTargets.AllPresent(
        PatchTargets.DropHintHandleBlocked,
        PatchTargets.DropHintDetachedSocket,
        PatchTargets.DropHintHighlighter,
        PatchTargets.HighlighterProjections,
        PatchTargets.TooltipHighlighterCreate,
        PatchTargets.HighlighterSequence,
        PatchTargets.HighlighterToPeak,
        PatchTargets.HighlighterToBase,
        PatchTargets.ElementProjectionData,
        PatchTargets.SocketBlockedSockets,
        PatchTargets.SocketIsBlocked,
        PatchTargets.SocketNestedElement,
        PatchTargets.SocketCompatibleElementInfo);
}

/// <summary>
/// One ghost per screw: a screw that already has a ghost in the pulse gets no second one. A fading
/// pulse rises again first, so every screw hinted now, new or already ghosted, gets a full peak.
/// </summary>
[HarmonyPatch]
internal static class DropHintOneGhostPatch
{
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, List<ElementProjection>> Ghosts =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, List<ElementProjection>>("highlightedProjections");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, Sequence?> Pulse =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, Sequence?>("transitionSequence");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, float> ToPeak =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, float>("toPeakDuration");
    private static readonly AccessTools.FieldRef<ProjectionHighlighterBase, float> ToBase =
        AccessTools.FieldRefAccess<ProjectionHighlighterBase, float>("toBaseDuration");

    private static bool Prepare() => DropHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipHighlighterCreate)!;

    private static bool Prefix(TooltipProjectionHighlighter __instance, Transform parent)
    {
        if (!DropHintFeature.Enabled.Value) return true;
        RiseAgain(__instance);
        foreach (var ghost in Ghosts(__instance))
        {
            if (ghost && ghost.transform.parent == parent) return false;
        }
        return true;
    }

    private static void RiseAgain(ProjectionHighlighterBase highlighter)
    {
        var pulse = Pulse(highlighter);
        if (pulse is null || !pulse.IsActive()) return;
        var elapsed = pulse.Elapsed(false);
        var position = PulseRewind.Position(elapsed, ToPeak(highlighter), ToBase(highlighter));
        if (position < elapsed) pulse.Goto(position, andPlay: true);
    }
}

/// <summary>After the game's pick, ghosts every screw the dropped part freed.</summary>
[HarmonyPatch]
internal static class DropHintFreedScrewsPatch
{
    private static bool Prepare() => DropHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DropHintHandleBlocked)!;

    private static void Postfix(ElementSocket? ___detachedSocket, TooltipProjectionHighlighter ___tooltipProjectionHighlighter)
    {
        if (!DropHintFeature.Enabled.Value || !___detachedSocket || !___tooltipProjectionHighlighter) return;

        foreach (var socket in ___detachedSocket!.BlockedSockets)
        {
            var screw = socket.NestedElement;
            if (socket.CompatibleElementInfo.Category != ElementCategory.Small || !screw ||
                screw.Info.Category != ElementCategory.Small || socket.IsBlocked)
                continue;
            ___tooltipProjectionHighlighter.CreateAndHighlightProjection(screw.ProjectionData, socket.transform);
        }
    }
}
