using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Devices;
using Restory.Gameplay.GameCursor;
using Rewired;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.Hitboxes;

/// <summary>
/// Minimum hitbox size at the workbench: with a gamepad, small things get hitboxes at least the
/// minimum across on screen (see docs/adr/0001). While a workbench hover state looks for what the
/// cursor points at, the game's hover ray reports the enlarged hitbox that wins instead of what it
/// hit, so the game's own selection takes it: nothing is deselected and selected again each frame,
/// and hover grace treats it like any other pick.
/// </summary>
internal sealed class HitboxEnlargingFeature : IFeature
{
    public static ConfigEntry<bool> Enabled = null!;
    public static ConfigEntry<float> MinimumSizePercent = null!;

    public FeatureManifest Manifest => FeatureManifests.HitboxEnlarging;

    public void Load(FeatureContext ctx)
    {
        Bind(ctx.Config);
        ctx.Patch(typeof(HitboxEnlargingDetectionPatch), typeof(HitboxEnlargingEmptyBenchPatch), typeof(HitboxEnlargingRayPatch));
    }

    private static void Bind(ConfigFile config)
    {
        Enabled = config.Bind(
            "Controller",
            "EnlargeSmallHitboxes",
            false,
            "Minimum hitbox size: with a gamepad, small things at the workbench (screws, screw slots, loose parts, attached parts, switches) get hitboxes at least MinimumHitboxSize across.");
        MinimumSizePercent = config.Bind(
            "Controller",
            "MinimumHitboxSize",
            MinimumHitboxSize.DefaultPercent,
            new ConfigDescription(
                "Minimum hitbox size, as a percentage of screen height. The hitbox overlay previews it even while EnlargeSmallHitboxes is off.",
                new AcceptableValueRange<float>(0f, MinimumHitboxSize.MaxPercent)));
    }

    /// <summary>The minimum hitbox size in pixels at the current screen height.</summary>
    public static float MinimumSizePixels =>
        Mathf.Clamp(MinimumSizePercent.Value, 0f, MinimumHitboxSize.MaxPercent) / 100f * Screen.height;

    /// <summary>Whether small hitboxes are enlarged this frame: switched on, and a gamepad drives the cursor.</summary>
    public static bool Active =>
        Enabled.Value && MinimumSizePixels > 0 && ReInput.isReady &&
        ReInput.controllers.GetLastActiveController()?.type == ControllerType.Joystick;
}

/// <summary>Enlarges hitboxes within one workbench hover state's detection.</summary>
internal static class HitboxEnlarging
{
    private static readonly WorkbenchHitboxes Scene = new();

    /// <summary>The frame whose detection is running, or -1 outside it.</summary>
    private static int detectingFrame = -1;

    private static int detectionMask;
    private static DeviceService? devices;

    /// <summary>A workbench hover state starts its detection.</summary>
    public static void Begin(LayerMask mask, DeviceService deviceService)
    {
        detectingFrame = HitboxEnlargingFeature.Active ? Time.frameCount : -1;
        detectionMask = mask.value;
        devices = deviceService;
    }

    public static void End() => detectingFrame = -1;

    /// <summary>
    /// After the game's hover ray: when an enlarged hitbox wins at the cursor, the ray reports a hit on
    /// that thing instead. The game only casts this ray once it has ruled out a panel under the
    /// cursor, a held press, rotating and waiting for a confirmation.
    /// </summary>
    public static void AfterHoverRay(Camera camera, Vector3 screenPoint, LayerMask mask, RaycastHit[] hits,
        ref int hitCount, ref bool found)
    {
        if (detectingFrame != Time.frameCount || mask.value != detectionMask || !camera) return;

        var minimumSize = HitboxEnlargingFeature.MinimumSizePixels;
        var cursor = new Vector2(screenPoint.x, screenPoint.y);
        Scene.Update(camera, detectionMask, devices, near: cursor, reach: minimumSize);

        var gamePick = Scene.GamePickFor(hits, hitCount);
        var pick = HitboxPicker.Pick(HitboxOverlay.ToCore(cursor), minimumSize, Scene.Sizings, gamePick, panelUnderCursor: false);
        if (pick.Reason != PickReason.Enlarged || pick.Thing is not { } winner) return;
        if (!WorkbenchHitboxes.HitOn(camera, winner, out var hit)) return;

        hits[0] = hit;
        hitCount = 1;
        found = true;
    }
}

/// <summary>The workbench with a device on it.</summary>
[HarmonyPatch]
internal static class HitboxEnlargingDetectionPatch
{
    private static bool Prepare() =>
        GameTargets.AllPresent(FeatureManifests.HitboxEnlarging,
            PatchTargets.DisassembleDetectionOnUpdate,
            PatchTargets.DisassembleDetectionMask,
            PatchTargets.DisassembleDetectionDeviceService)
        && WorkbenchHitboxes.Prepare(FeatureManifests.HitboxEnlarging);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.DisassembleDetectionOnUpdate)!;

    private static void Prefix(LayerMask ___detectionLayerMask, DeviceService ___deviceService) =>
        HitboxEnlarging.Begin(___detectionLayerMask, ___deviceService);

    private static void Postfix() => HitboxEnlarging.End();
}

/// <summary>The workbench with no device on it, where loose parts can still lie.</summary>
[HarmonyPatch]
internal static class HitboxEnlargingEmptyBenchPatch
{
    private static bool Prepare() =>
        GameTargets.AllPresent(FeatureManifests.HitboxEnlarging,
            PatchTargets.EmptyDetectionOnUpdate,
            PatchTargets.EmptyDetectionMask,
            PatchTargets.EmptyDetectionDeviceService)
        && WorkbenchHitboxes.Prepare(FeatureManifests.HitboxEnlarging);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.EmptyDetectionOnUpdate)!;

    private static void Prefix(LayerMask ___detectionLayerMask, DeviceService ___deviceService) =>
        HitboxEnlarging.Begin(___detectionLayerMask, ___deviceService);

    private static void Postfix() => HitboxEnlarging.End();
}

/// <summary>The game's hover ray, shared by every state that looks under the cursor.</summary>
[HarmonyPatch]
internal static class HitboxEnlargingRayPatch
{
    private static MethodBase? target;

    private static bool Prepare()
    {
        if (!GameTargets.AllPresent(FeatureManifests.HitboxEnlarging, PatchTargets.CursorDetectorTryToDetect) || !HoverCapture.Prepare(FeatureManifests.HitboxEnlarging)) return false;
        target ??= AccessTools.Method(typeof(GameCursorDetector), PatchTargets.CursorDetectorTryToDetect.MemberName,
            [typeof(Vector3), typeof(LayerMask), typeof(RaycastHit[]), typeof(int).MakeByRefType()]);
        if (target is null) Plugin.Logger.LogWarning($"Patch target missing: {PatchTargets.CursorDetectorTryToDetect} (ray hits overload)");
        return target is not null;
    }

    private static MethodBase TargetMethod() => target!;

    private static void Postfix(GameCursorDetector __instance, Vector3 screenPoint, LayerMask layerMask,
        RaycastHit[] raycastHits, ref int hitCount, ref bool __result) =>
        HitboxEnlarging.AfterHoverRay(HoverCapture.CameraOf(__instance), screenPoint, layerMask, raycastHits,
            ref hitCount, ref __result);
}
