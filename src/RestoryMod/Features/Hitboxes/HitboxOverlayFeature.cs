using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Elements;
using Restory.Gameplay.GameCursor;
using Restory.Gameplay.PlayerInput;
using RestoryMod.Core;
using UnityEngine;
using MiniGameAction = RewiredConsts.Action.MiniGame;

namespace RestoryMod.Features.Hitboxes;

/// <summary>
/// Hitbox overlay: a debug view at the workbench that draws native and enlarged hitboxes over the
/// game and lists what lies under the cursor. It only reads; enlarging is HitboxEnlargingFeature.
/// </summary>
internal sealed class HitboxOverlayFeature : IFeature
{
    public static ConfigEntry<bool> Show = null!;

    public FeatureManifest Manifest => FeatureManifests.HitboxOverlay;

    public void Load(FeatureContext ctx)
    {
        Show = ctx.Config.Bind(
            "Debug",
            "ShowHitboxes",
            false,
            "Hitbox overlay: draw hitboxes at the workbench over the game, with a readout of what lies under the cursor.");
        ctx.Patch(typeof(HitboxOverlayDetectionPatch), typeof(HitboxOverlayEmptyBenchPatch));
        // Idle unless ShowHitboxes is on and a workbench hover state recorded this frame.
        ctx.AddPoller<HitboxOverlay>();
    }
}

/// <summary>What a workbench hover state saw in its latest frame.</summary>
internal sealed class HoverSnapshot
{
    public int Frame = -1;
    public Camera? Camera;
    public DeviceService? Devices;
    public int LayerMask;

    /// <summary>How many ray hits the game keeps; it drops the rest, in no particular order.</summary>
    public int GameHitLimit;

    public Vector2 Cursor;

    /// <summary>What the game hovers: an <see cref="ElementProjection"/>, <see cref="ElementButton"/> or <see cref="ElementBase"/>.</summary>
    public Component? GamePick;

    public bool PanelUnderCursor;

    /// <summary>Why the game is not looking for something to hover this frame, if it is not.</summary>
    public string? NotPicking;
}

/// <summary>Records the workbench hover states' results for the overlay, while it is shown.</summary>
internal static class HoverCapture
{
    public static readonly HoverSnapshot Latest = new();

    private static AccessTools.FieldRef<GameCursorDetector, Camera>? gameCamera;

    public static bool Prepare(FeatureManifest feature)
    {
        if (!GameTargets.AllPresent(feature, PatchTargets.CursorDetectorCamera)) return false;
        gameCamera ??= AccessTools.FieldRefAccess<GameCursorDetector, Camera>(PatchTargets.CursorDetectorCamera.MemberName!);
        return true;
    }

    /// <summary>The camera the game's hover ray is cast from.</summary>
    public static Camera CameraOf(GameCursorDetector detector) => gameCamera!(detector);

    public static void Record(
        CursorDetectorService detectors,
        DeviceService devices,
        LayerMask mask,
        RaycastHit[] gameHits,
        IPlayerInput input,
        CursorSelectionService selection,
        Component? gamePick,
        string? notPicking)
    {
        var snapshot = Latest;
        snapshot.Frame = Time.frameCount;
        snapshot.Camera = CameraOf(detectors.GameDetector);
        snapshot.Devices = devices;
        snapshot.LayerMask = mask.value;
        snapshot.GameHitLimit = gameHits.Length;
        snapshot.Cursor = input.GetMousePosition();
        snapshot.GamePick = gamePick;
        // The game reports UI under the cursor (a panel, the parts box) as a detected RectTransform.
        var detected = selection.DetectedGameObject;
        snapshot.PanelUnderCursor = detected && detected.TryGetComponent<RectTransform>(out _);
        snapshot.NotPicking = notPicking;
    }
}

/// <summary>The workbench with a device on it.</summary>
[HarmonyPatch]
internal static class HitboxOverlayDetectionPatch
{
    private static bool Prepare() =>
        GameTargets.AllPresent(FeatureManifests.HitboxOverlay,
            PatchTargets.DisassembleDetectionOnUpdate,
            PatchTargets.DisassembleDetectionPlayerInput,
            PatchTargets.DisassembleDetectionDetector,
            PatchTargets.DisassembleDetectionMask,
            PatchTargets.DisassembleDetectionHits,
            PatchTargets.DisassembleDetectionSelection,
            PatchTargets.DisassembleDetectionRotation,
            PatchTargets.DisassembleDetectionWaiting,
            PatchTargets.DisassembleDetectionElement,
            PatchTargets.DisassembleDetectionProjection,
            PatchTargets.DisassembleDetectionButton,
            PatchTargets.DisassembleDetectionDeviceService)
        && HoverCapture.Prepare(FeatureManifests.HitboxOverlay)
        && WorkbenchHitboxes.Prepare(FeatureManifests.HitboxOverlay);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DisassembleDetectionOnUpdate)!;

    // After hover grace, so the snapshot holds what the cursor really hovers this frame.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        IPlayerInput ___playerInput,
        CursorDetectorService ___cursorDetectorService,
        DeviceService ___deviceService,
        LayerMask ___detectionLayerMask,
        RaycastHit[] ___raycastHits,
        CursorSelectionService ___cursorSelectionService,
        DisassembleRotationController ___rotationController,
        bool ___isWaitingForConfirmation,
        ElementBase? ___selectedElement,
        ElementProjection? ___selectedProjection,
        ElementButton? ___selectedButton)
    {
        if (!HitboxOverlayFeature.Show.Value) return;

        Component? gamePick = ___selectedElement;
        if (___selectedButton) gamePick = ___selectedButton;
        if (___selectedProjection) gamePick = ___selectedProjection;

        // The game keeps its selection as it is in these cases instead of looking again.
        string? notPicking = null;
        if (___isWaitingForConfirmation) notPicking = "waiting for a confirmation";
        else if (___playerInput.GetButton(MiniGameAction.Activate)) notPicking = "press held";
        else if (___rotationController && ___rotationController.IsRotationButtonPressed) notPicking = "rotating";

        HoverCapture.Record(___cursorDetectorService, ___deviceService, ___detectionLayerMask, ___raycastHits,
            ___playerInput, ___cursorSelectionService, gamePick, notPicking);
    }
}

/// <summary>The workbench with no device on it, where loose parts can still lie.</summary>
[HarmonyPatch]
internal static class HitboxOverlayEmptyBenchPatch
{
    private static bool Prepare() =>
        GameTargets.AllPresent(FeatureManifests.HitboxOverlay,
            PatchTargets.EmptyDetectionOnUpdate,
            PatchTargets.EmptyDetectionPlayerInput,
            PatchTargets.EmptyDetectionDetector,
            PatchTargets.EmptyDetectionMask,
            PatchTargets.EmptyDetectionHits,
            PatchTargets.EmptyDetectionSelection,
            PatchTargets.EmptyDetectionElement,
            PatchTargets.EmptyDetectionDeviceService)
        && HoverCapture.Prepare(FeatureManifests.HitboxOverlay)
        && WorkbenchHitboxes.Prepare(FeatureManifests.HitboxOverlay);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.EmptyDetectionOnUpdate)!;

    private static void Postfix(
        IPlayerInput ___playerInput,
        CursorDetectorService ___cursorDetectorService,
        DeviceService ___deviceService,
        LayerMask ___detectionLayerMask,
        RaycastHit[] ___raycastHits,
        CursorSelectionService ___cursorSelectionService,
        ElementBase? ___selectedElement)
    {
        if (!HitboxOverlayFeature.Show.Value) return;

        HoverCapture.Record(___cursorDetectorService, ___deviceService, ___detectionLayerMask, ___raycastHits,
            ___playerInput, ___cursorSelectionService, ___selectedElement ? ___selectedElement : null, notPicking: null);
    }
}
