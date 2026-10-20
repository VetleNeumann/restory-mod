using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Devices;
using Restory.Gameplay.PlayerInput;
using RestoryMod.Core;

namespace RestoryMod.Features.ZoomToggle;

/// <summary>
/// At the workbench, L2 toggles the zoom (in when out, out when in) instead of the triggers zooming
/// in and out while held; R2 is free for the screw hint. The toggle goes through the game's own zoom
/// update as a full-strength zoom input, so wherever the game ignores zoom (over the Notepad or the
/// parts box, the PC, cleaning transitions) it ignores the toggle too. The mouse wheel is untouched
/// unless a trigger is held at the same time.
/// </summary>
internal static class ZoomToggleFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Controller", "TriggerZoomToggle", true,
            "At the workbench, L2 toggles between zoomed in and out instead of L2/R2 zooming while held. " +
            "R2 is then free for the screw hint.");
    }

    public static bool TargetsPresent() => GameTargets.AllPresent(FeatureManifests.ZoomToggle);

    /// <summary>
    /// Zoom input for one frame large enough that the game, after clamping it to its per-frame limit,
    /// still zooms a long way; <see cref="ZoomToggleUpdatePatch"/> then finishes the trip.
    /// </summary>
    private const float FullZoomInput = 1000f;

    /// <summary>True while the zoom controller reads its axis, so the axis patch touches nothing else.</summary>
    internal static bool InZoomUpdate;
    internal static int ZoomAxisId;
    /// <summary>The toggle's zoom direction for this update (+1 in, -1 out), or 0 to only mute the triggers.</summary>
    internal static int ToggleDirection;

    internal static float ForcedZoomInput => ToggleDirection * FullZoomInput;

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Zoom toggle failed (further errors suppressed): {e}");
    }
}

/// <summary>
/// Around the zoom controller's per-frame update: an L2 press becomes a zoom input in the toggle's
/// direction, and when the game took it, the zoom goes all the way (the game's keyboard step is small).
/// </summary>
[HarmonyPatch]
internal static class ZoomToggleUpdatePatch
{
    private static readonly AccessTools.FieldRef<DeviceZoomController, float> Target =
        AccessTools.FieldRefAccess<DeviceZoomController, float>(PatchTargets.ZoomTarget.MemberName);
    private static readonly AccessTools.FieldRef<DeviceZoomController, int> AxisId =
        AccessTools.FieldRefAccess<DeviceZoomController, int>(PatchTargets.ZoomAxisId.MemberName);

    private static bool Prepare() => ZoomToggleFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ZoomUpdateTarget)!;

    private static void Prefix(DeviceZoomController __instance, out float __state)
    {
        __state = 0f;
        ZoomToggleFeature.InZoomUpdate = false;
        ZoomToggleFeature.ToggleDirection = 0;
        if (!ZoomToggleFeature.Enabled.Value) return;
        try
        {
            __state = Target(__instance);
            ZoomToggleFeature.ZoomAxisId = AxisId(__instance);
            GamepadTriggers.Left(out var pressed, out _);
            if (pressed) ZoomToggleFeature.ToggleDirection = Core.ZoomToggle.Direction(__state);
            ZoomToggleFeature.InZoomUpdate = true;
        }
        catch (Exception e)
        {
            ZoomToggleFeature.LogOnce(e);
        }
    }

    private static void Postfix(DeviceZoomController __instance, float __state)
    {
        var direction = ZoomToggleFeature.ToggleDirection;
        ZoomToggleFeature.InZoomUpdate = false;
        ZoomToggleFeature.ToggleDirection = 0;
        // Moved at all means the game took the input (nothing was blocking zoom): finish the trip.
        if (direction != 0 && Target(__instance) != __state) Target(__instance) = Core.ZoomToggle.End(direction);
    }
}

/// <summary>The zoom axis as the zoom controller reads it: the toggle's input, or nothing while a trigger is off its rest.</summary>
[HarmonyPatch]
internal static class ZoomToggleAxisPatch
{
    private static MethodBase? target;

    // GetAxisRaw has an int and a string overload; the zoom controller uses the int one.
    private static bool Prepare()
    {
        if (!ZoomToggleFeature.TargetsPresent()) return false;
        target ??= AccessTools.Method(typeof(RewiredPlayerInput), PatchTargets.PlayerInputGetAxisRaw.MemberName, [typeof(int)]);
        if (target is null) Plugin.Logger.LogWarning($"Patch target missing: {PatchTargets.PlayerInputGetAxisRaw} (int overload)");
        return target is not null;
    }

    private static MethodBase TargetMethod() => target!;

    private static bool Prefix(int actionId, ref float __result)
    {
        if (!ZoomToggleFeature.InZoomUpdate || actionId != ZoomToggleFeature.ZoomAxisId) return true;
        try
        {
            if (ZoomToggleFeature.ToggleDirection != 0)
            {
                __result = ZoomToggleFeature.ForcedZoomInput;
                return false;
            }
            if (!GamepadTriggers.AnyMoved()) return true;
            __result = 0f;
            return false;
        }
        catch (Exception e)
        {
            ZoomToggleFeature.LogOnce(e);
            return true;
        }
    }
}
