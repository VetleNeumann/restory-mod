using System;
using System.Reflection;
using HarmonyLib;
using Restory.Data.Elements.Condition;
using Restory.Data.Outline;
using Restory.Gameplay.Common;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using RestoryMod.Features.DropHint;

namespace RestoryMod.Features.HoverOutline;

/// <summary>
/// The hover outline on a ready part shows its condition (burnt, dirty, damaged) while it is still on
/// the device, and the hover counts as having seen it. The game shows a condition only on a part lying
/// on the bench, so a part had to come out just to find out it was fine.
/// </summary>
[HarmonyPatch]
internal static class ConditionHoverPatch
{
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.ConditionHover);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ElementViewResolveSelection)!;

    private static void Postfix(ElementBase ___element, OutlinableAdapter ___outlineAdapter,
        ElementOutlineSettings ___outlineSettings)
    {
        if (!___element || !___outlineAdapter || !___outlineAdapter.IsActive || !___outlineSettings) return;
        if (___element.IsOnSurface || ___element.IsInstalling || ___element.IsBlocked) return;
        try
        {
            if (!EveryPartHintFeature.IsReadyToTakeOff(___element)) return;
            var data = ___element.ConditionHandler.ElementData;
            data.IsInspected = true;
            var preset = data.Condition switch
            {
                BurntElementCondition => ___outlineSettings.BurntElementOutline,
                DirtyElementCondition => ___outlineSettings.DirtyElementOutline,
                DamagedElementCondition => ___outlineSettings.DamagedElementOutline,
                _ => null,
            };
            if (preset) ___outlineAdapter.OverridePreset = preset;
        }
        catch (Exception e)
        {
            EveryPartHintFeature.LogOnce(e);
        }
    }
}
