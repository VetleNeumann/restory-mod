using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Disassemble;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Elements;
using Restory.Gameplay.GameCursor;
using Restory.Gameplay.InteractiveObjects;
using Restory.Gameplay.PlayerInput;
using Restory.Gameplay.Work.StateMachine;
using Rewired;
using RestoryMod.Core;
using UnityEngine;
using MiniGameAction = RewiredConsts.Action.MiniGame;

namespace RestoryMod.Features.HoverGrace;

/// <summary>
/// Hover grace at the workbench: with a gamepad, the thing the virtual cursor just left stays
/// hovered for a moment, so a press just after overshooting still acts on it. Both hover states
/// (the bench, and the device being worked on) keep their hover in a stored selection that the
/// Activate press reads; after each frame's detection, the patches put the lingering thing back
/// when the game found nothing else.
/// </summary>
internal static class HoverGraceFeature
{
    public static ConfigEntry<int> WindowMs = null!;

    public static void Bind(ConfigFile config)
    {
        WindowMs = config.Bind(
            "Controller",
            "HoverGraceMs",
            HoverGraceWindow.DefaultMs,
            new ConfigDescription(
                "Hover grace: how long (ms) a part, screw or bench object stays hovered after the gamepad cursor leaves it. 0 = off.",
                new AcceptableValueRange<int>(0, HoverGraceWindow.MaxMs)));
    }

    /// <summary>This frame's window in seconds; 0 unless a gamepad drives the cursor.</summary>
    public static float Window =>
        ReInput.isReady && ReInput.controllers.GetLastActiveController()?.type == ControllerType.Joystick
            ? Mathf.Clamp(WindowMs.Value, 0, HoverGraceWindow.MaxMs) / 1000f
            : 0f;

    /// <summary>Feeds this frame's detection result to <paramref name="grace"/>.</summary>
    public static T? Next<T>(HoverGrace<T> grace, HoverKind kind, T? hovered) where T : class =>
        grace.Next(Time.frameCount, Time.unscaledTime, kind, hovered, Window);
}

/// <summary>
/// Inside the device: loose parts, screws, and the slots screws go back into (the only projections
/// the cursor can hover). Attached parts and buttons count as something else; an attached part
/// that is blocked (e.g. a casing still screwed down) counts as nothing, since a press on it does
/// nothing, and it is what the cursor lands on after overshooting a screw.
/// </summary>
[HarmonyPatch]
internal static class HoverGraceDisassemblePatch
{
    private static readonly HoverGrace<Component> Grace = new();
    private static Action<DetectionDisassembleState>? resetElement;
    private static Action<DetectionDisassembleState, ElementBase>? setElement;

    private static bool Prepare()
    {
        if (!GameTargets.AllPresent(
                PatchTargets.DisassembleDetectionOnUpdate,
                PatchTargets.DisassembleDetectionPlayerInput,
                PatchTargets.DisassembleDetectionSelection,
                PatchTargets.DisassembleDetectionRotation,
                PatchTargets.DisassembleDetectionWaiting,
                PatchTargets.DisassembleDetectionElement,
                PatchTargets.DisassembleDetectionProjection,
                PatchTargets.DisassembleDetectionButton,
                PatchTargets.DisassembleDetectionResetElement,
                PatchTargets.DisassembleDetectionSetElement))
            return false;

        resetElement = AccessTools.MethodDelegate<Action<DetectionDisassembleState>>(
            GameTargets.Method(PatchTargets.DisassembleDetectionResetElement));
        setElement = AccessTools.MethodDelegate<Action<DetectionDisassembleState, ElementBase>>(
            GameTargets.Method(PatchTargets.DisassembleDetectionSetElement));
        return true;
    }

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DisassembleDetectionOnUpdate)!;

    private static void Postfix(
        DetectionDisassembleState __instance,
        IPlayerInput ___playerInput,
        CursorSelectionService ___cursorSelectionService,
        DisassembleRotationController ___rotationController,
        bool ___isWaitingForConfirmation,
        ElementBase? ___selectedElement,
        ref ElementProjection? ___selectedProjection,
        ElementButton? ___selectedButton)
    {
        // The game skips detection in these cases too, keeping its selection as it is. A press
        // uses up the grace.
        if (___isWaitingForConfirmation || ___playerInput.GetButton(MiniGameAction.Activate))
        {
            Grace.Forget();
            return;
        }

        Component? hovered = ___selectedProjection ? ___selectedProjection : ___selectedElement;
        var kind = Classify(___cursorSelectionService, ___rotationController, ___selectedElement, ___selectedProjection, ___selectedButton);
        var keep = HoverGraceFeature.Next(Grace, kind, kind == HoverKind.Eligible ? hovered : null);
        if (!keep || !keep!.gameObject.activeInHierarchy) return;

        if (keep is ElementProjection projection)
        {
            resetElement!(__instance);
            ___selectedProjection = projection;
            projection.MakeBright();
            ___cursorSelectionService.SetDetection(projection.gameObject);
        }
        else if (keep is ElementBase element && element.IsActivated)
        {
            setElement!(__instance, element);
        }
    }

    private static HoverKind Classify(
        CursorSelectionService selection,
        DisassembleRotationController rotation,
        ElementBase? element,
        ElementProjection? projection,
        ElementButton? button)
    {
        if (rotation && rotation.IsRotationButtonPressed) return HoverKind.Other;
        if (projection) return HoverKind.Eligible;
        if (button) return HoverKind.Other;
        if (element)
        {
            if (!element.InSocket || element.Info.Category == ElementCategory.Small) return HoverKind.Eligible;
            return element.IsBlocked && element is not ISwitchableElement ? HoverKind.Nothing : HoverKind.Other;
        }
        // The game reports UI under the cursor (a panel, the parts box) as a detected RectTransform.
        var detected = selection.DetectedGameObject;
        return detected && detected.TryGetComponent<RectTransform>(out _) ? HoverKind.Other : HoverKind.Nothing;
    }
}

/// <summary>
/// On the bench: every object the game selects there (device boxes, packs, cash, decor) is covered.
/// Anything else the game detects (UI, levers, the cash register) counts as something else.
/// </summary>
[HarmonyPatch]
internal static class HoverGraceWorkPatch
{
    private static readonly HoverGrace<InteractiveObject> Grace = new();
    private static Action<DetectionWorkState, InteractiveObject>? selectObject;
    private static Func<DetectionWorkState, bool>? visitsBlockDetection;

    private static bool Prepare()
    {
        if (!GameTargets.AllPresent(
                PatchTargets.DetectionWorkStateOnUpdate,
                PatchTargets.DetectionWorkStatePlayerInput,
                PatchTargets.WorkDetectionSelection,
                PatchTargets.WorkDetectionObject,
                PatchTargets.WorkDetectionSelectObject,
                PatchTargets.WorkDetectionVisitsBlock))
            return false;

        selectObject = AccessTools.MethodDelegate<Action<DetectionWorkState, InteractiveObject>>(
            GameTargets.Method(PatchTargets.WorkDetectionSelectObject));
        visitsBlockDetection = AccessTools.MethodDelegate<Func<DetectionWorkState, bool>>(
            GameTargets.Method(PatchTargets.WorkDetectionVisitsBlock));
        return true;
    }

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DetectionWorkStateOnUpdate)!;

    private static void Postfix(
        DetectionWorkState __instance,
        IPlayerInput ___playerInput,
        CursorSelectionService ___cursorSelectionService,
        InteractiveObject? ___selectedObject)
    {
        // The game skips detection while Activate is held or released, so a drag or an
        // activation resolves on what was hovered at the press. A press uses up the grace.
        if (___playerInput.GetButton(MiniGameAction.Activate) || ___playerInput.GetButtonUp(MiniGameAction.Activate))
        {
            Grace.Forget();
            return;
        }

        var kind = ___selectedObject ? HoverKind.Eligible
            : visitsBlockDetection!(__instance) || ___cursorSelectionService.DetectedGameObject ? HoverKind.Other
            : HoverKind.Nothing;
        var keep = HoverGraceFeature.Next(Grace, kind, kind == HoverKind.Eligible ? ___selectedObject : null);
        if (!keep || !keep!.IsInteractable || !keep.gameObject.activeInHierarchy) return;

        selectObject!(__instance, keep);
    }
}
