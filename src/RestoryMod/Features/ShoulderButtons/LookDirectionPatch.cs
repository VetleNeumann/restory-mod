using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Common;
using Restory.Gameplay.GameView;
using Restory.Gameplay.PlayerInput;
using RestoryMod.Core;

namespace RestoryMod.Features.ShoulderButtons;

/// <summary>
/// L2/R2 step the Look direction away from the workbench. The game checks for the mouse at the
/// screen edges in its work states' per-frame update (not the workbench's, where L2 zooms and R2 hints);
/// ours runs right after that check, under the same blockers (PC, Notepad, inventory, dialogue, payment).
/// </summary>
[HarmonyPatch]
internal static class LookDirectionPatch
{
    private static AccessTools.FieldRef<CameraDirectionSwitcher, ActiveStateSwitcher>? blockers;

    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.ShoulderButtons,
        PatchTargets.DetectionWorkStateOnUpdate,
        PatchTargets.DetectionWorkStatePlayerInput,
        PatchTargets.DetectionWorkStateCameraDirectionSwitcher,
        PatchTargets.DraggingWorkStateOnUpdate,
        PatchTargets.DraggingWorkStatePlayerInput,
        PatchTargets.DraggingWorkStateCameraDirectionSwitcher,
        PatchTargets.CameraDirectionSwitcherBlockers);

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return GameTargets.Method(PatchTargets.DetectionWorkStateOnUpdate)!;
        yield return GameTargets.Method(PatchTargets.DraggingWorkStateOnUpdate)!;
    }

    private static void Postfix(IPlayerInput ___playerInput, CameraDirectionSwitcher ___cameraDirectionSwitcher)
    {
        try
        {
            if (!ShoulderButtonsFeature.LookEnabled.Value) return;
            if (___playerInput is null || !___cameraDirectionSwitcher) return;

            var step = ___playerInput.GetButtonDown(ShoulderButtonsFeature.LeftActionId) ? -1
                : ___playerInput.GetButtonDown(ShoulderButtonsFeature.RightActionId) ? 1
                : 0;
            if (step == 0 || ShoulderButtonsFeature.IsPaused()) return;
            blockers ??= AccessTools.FieldRefAccess<CameraDirectionSwitcher, ActiveStateSwitcher>(
                PatchTargets.CameraDirectionSwitcherBlockers.MemberName!);
            if (!blockers(___cameraDirectionSwitcher).ShouldSystemBeActive) return;
            if (ToLook(___cameraDirectionSwitcher.CurrentDirection) is not { } current) return;

            var next = ShoulderStep.Look(current, step);
            if (next != current) ___cameraDirectionSwitcher.ApplyMainCameraDirection(ToGame(next));
        }
        catch (Exception e)
        {
            ShoulderButtonsFeature.LogOnce(e);
        }
    }

    private static LookDirection? ToLook(CameraDirection direction) => direction switch
    {
        CameraDirection.Left => LookDirection.Left,
        CameraDirection.Main => LookDirection.Main,
        CameraDirection.Right => LookDirection.Right,
        _ => null,
    };

    private static CameraDirection ToGame(LookDirection direction) => direction switch
    {
        LookDirection.Left => CameraDirection.Left,
        LookDirection.Right => CameraDirection.Right,
        _ => CameraDirection.Main,
    };
}
