using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Common;
using Restory.Gameplay.Elements;
using RestoryMod.Core;

namespace RestoryMod.Features.HoverOutline;

internal sealed class BlockedHoverFeature : IFeature
{
    public FeatureManifest Manifest => FeatureManifests.BlockedHover;

    public void Load(FeatureContext ctx) => ctx.Patch(typeof(BlockedHoverPatch));
}

/// <summary>
/// No hover outline on a blocked part. Most prefabs keep a blocked part from being hovered at all;
/// the rest (selectableWhenBlocked) let the game outline it in its "can't act" red. After the game
/// resolves the outline on a selection change, it is switched off again on a blocked part, so every
/// device behaves the same. Switches keep theirs, and a part being installed is left alone. Screws
/// reach this through the base call in ThreadedElementView; switch views resolve their own.
/// </summary>
[HarmonyPatch]
internal static class BlockedHoverPatch
{
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.BlockedHover);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ElementViewResolveSelection)!;

    private static void Postfix(ElementBase ___element, OutlinableAdapter ___outlineAdapter)
    {
        if (!___element || !___outlineAdapter || !___outlineAdapter.IsActive) return;
        if (___element.IsInstalling) return;
        if (Core.HoverOutline.Shows(___element.IsBlocked, ___element is ISwitchableElement)) return;

        ___outlineAdapter.IsActive = false;
    }
}
