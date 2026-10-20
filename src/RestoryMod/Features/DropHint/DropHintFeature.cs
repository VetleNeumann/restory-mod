using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Elements;
using RestoryMod.Core;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The drop hint: when a removed part is dropped, the game pulses yellow ghosts over the screws that
/// are now the last thing holding some part. The mod also ghosts every screw the dropped part freed.
/// </summary>
internal static class DropHintFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Workbench", "DropHintEveryFreedScrew", true,
            "When you drop a part you removed, the yellow pulse also covers every screw that part freed, " +
            "not only screws that are the last thing holding another part.");
    }

    public static bool TargetsPresent() => GameTargets.AllPresent(FeatureManifests.DropHint);
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
