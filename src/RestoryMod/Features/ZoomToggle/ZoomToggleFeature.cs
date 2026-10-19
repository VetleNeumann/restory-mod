using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Devices;
using RestoryMod.Core;

namespace RestoryMod.Features.ZoomToggle;

/// <summary>
/// At the workbench, L2 toggles the zoom (in when out, out when in) instead of the triggers zooming
/// in and out while held; R2 is free for the screw hint. The toggle goes through the game's own zoom
/// update as a full-strength zoom input, so wherever the game ignores zoom (over the Notepad or the
/// parts box, the PC, cleaning transitions) it ignores the toggle too. The mouse wheel is untouched.
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

    public static bool TargetsPresent() => GameTargets.AllPresent(
        PatchTargets.ZoomUpdateTarget,
        PatchTargets.ZoomAxisId,
        PatchTargets.ZoomTarget,
        PatchTargets.PlayerInputGetAxisRaw);

    /// <summary>True while the zoom controller reads its axis, so the axis patch touches nothing else.</summary>
    internal static bool InZoomUpdate;
    internal static int ZoomAxisId;
    /// <summary>The toggle's zoom input for this update, or null to only mute the triggers.</summary>
    internal static float? Forced;

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Zoom toggle failed (further errors suppressed): {e}");
    }
}

/// <summary>Around the zoom controller's per-frame update: an L2 press becomes a full zoom in or out.</summary>
[HarmonyPatch]
internal static class ZoomToggleUpdatePatch
{
    private static readonly AccessTools.FieldRef<DeviceZoomController, float> Target =
        AccessTools.FieldRefAccess<DeviceZoomController, float>("targetZoom");
    private static readonly AccessTools.FieldRef<DeviceZoomController, int> AxisId =
        AccessTools.FieldRefAccess<DeviceZoomController, int>("zoomAxisId");

    private static bool Prepare() => ZoomToggleFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ZoomUpdateTarget)!;

    private static void Prefix(DeviceZoomController __instance)
    {
        ZoomToggleFeature.InZoomUpdate = false;
        if (!ZoomToggleFeature.Enabled.Value) return;
        try
        {
            ZoomToggleFeature.ZoomAxisId = AxisId(__instance);
            // The game clamps a frame's input to its notch limit, which still zooms all the way.
            ZoomToggleFeature.Forced = GamepadTriggers.LeftPressed()
                ? TriggerButton.ToggleDirection(Target(__instance)) * 1000f
                : null;
            ZoomToggleFeature.InZoomUpdate = true;
        }
        catch (Exception e)
        {
            ZoomToggleFeature.LogOnce(e);
        }
    }

    private static void Postfix() => ZoomToggleFeature.InZoomUpdate = false;
}

/// <summary>The zoom axis as the zoom controller reads it: the toggle's input, or nothing while a trigger is held.</summary>
[HarmonyPatch]
internal static class ZoomToggleAxisPatch
{
    private static bool Prepare() => ZoomToggleFeature.TargetsPresent();

    // GetAxisRaw has an int and a string overload; the zoom controller uses the int one.
    private static MethodBase TargetMethod() => AccessTools.Method(
        AccessTools.TypeByName(PatchTargets.PlayerInputGetAxisRaw.TypeName),
        PatchTargets.PlayerInputGetAxisRaw.MemberName, new[] { typeof(int) });

    private static bool Prefix(int actionId, ref float __result)
    {
        if (!ZoomToggleFeature.InZoomUpdate || actionId != ZoomToggleFeature.ZoomAxisId) return true;
        if (ZoomToggleFeature.Forced is { } forced)
        {
            __result = forced;
            return false;
        }
        if (!GamepadTriggers.AnyHeld()) return true;
        __result = 0f;
        return false;
    }
}
