using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// Keeps the game's taking-apart mode through a tube's shift (TubeMode): without it, taking one battery
/// out of the Lynx switches the hint to putting back while the device still holds parts needing work.
/// </summary>
[HarmonyPatch]
internal static class TubeModePatch
{
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, bool> Disassembling =
        AccessTools.FieldRefAccess<DisassembleTooltipService, bool>(PatchTargets.TooltipServiceIsDisassembling.MemberName);
    private static readonly AccessTools.FieldRef<SerialElementSocket, List<SubordinateElementSocket>> Places =
        AccessTools.FieldRefAccess<SerialElementSocket, List<SubordinateElementSocket>>(PatchTargets.SerialSubordinates.MemberName);

    /// <summary>Parts in each tube at its last change.</summary>
    private static readonly Dictionary<SerialElementSocket, int> Counts = new();

    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.TubeMode);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipServiceSocketChanged)!;

    private static void Prefix(DisassembleTooltipService __instance, out bool __state) => __state = Disassembling(__instance);

    private static void Postfix(DisassembleTooltipService __instance, ElementSocket socket, bool __state)
    {
        if (socket is not SerialElementSocket tube || !tube) return;
        try
        {
            var now = (tube.NestedElement ? 1 : 0) + Places(tube).Count(p => p && p.NestedElement);
            int? before = Counts.TryGetValue(tube, out var n) ? n : null;
            Counts[tube] = now;
            // Only a "putting back" from the game can be a shift; its "taking apart" (a whole device, an
            // emptied socket) stands.
            if (!Disassembling(__instance)) Disassembling(__instance) = TubeMode.TakingApart(__state, before, now, gameSays: false);
        }
        catch (Exception e)
        {
            HintPulse.LogOnce(e);
        }
    }
}
