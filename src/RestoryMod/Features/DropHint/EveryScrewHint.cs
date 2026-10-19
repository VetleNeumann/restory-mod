using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The game's workbench hint (Space, or R2 with the mod) pulses only the next part's screws. With
/// this it pulses every screw that can come out now while taking the device apart, and every empty
/// screw slot that can take its screw now while putting it back. The game still decides which of the
/// two you are doing; when there is no screw to show, its own hint runs (some parts have special ones).
/// </summary>
internal static class EveryScrewHintFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Workbench", "HintShowsEveryScrew", true,
            "The game's hint at the workbench (Space, or R2 on a gamepad) pulses every screw you can unscrew " +
            "now while taking a device apart, and every empty screw slot you can screw into now while putting it " +
            "back, instead of only the next part's.");
    }

    public static bool TargetsPresent() => GameTargets.AllPresent(
        PatchTargets.TooltipHighlighterCreate,
        PatchTargets.ElementProjectionData,
        PatchTargets.SocketIsBlocked,
        PatchTargets.SocketNestedElement,
        PatchTargets.SocketCompatibleElementInfo,
        PatchTargets.HintShowDisassembling,
        PatchTargets.HintShowAssembling,
        PatchTargets.HintHighlighter,
        PatchTargets.DeviceElementSockets,
        PatchTargets.SocketIsAvailable,
        PatchTargets.SocketLastNestedElement);

    /// <summary>
    /// A prefix's body: ghosts the screws (or screw slots) <paramref name="wanted"/> picks, and returns
    /// whether the game's own hint should still run (off, or no screw to show).
    /// </summary>
    public static bool RunGameHint(Device device, TooltipProjectionHighlighter highlighter, Func<ElementSocket, ElementBase?> wanted)
    {
        if (!Enabled.Value || !device || !highlighter) return true;
        try
        {
            return !Show(device, highlighter, wanted);
        }
        catch (Exception e)
        {
            LogOnce(e);
            return true;
        }
    }

    private static bool Show(Device device, TooltipProjectionHighlighter highlighter, Func<ElementSocket, ElementBase?> wanted)
    {
        var any = false;
        foreach (var socket in device.ElementSockets)
        {
            if (socket.CompatibleElementInfo.Category != ElementCategory.Small) continue;
            var screw = wanted(socket);
            if (!screw || screw!.Info.Category != ElementCategory.Small) continue;
            highlighter.CreateAndHighlightProjection(screw.ProjectionData, socket.transform);
            any = true;
        }
        return any;
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, then lets the game's own hint run.</summary>
    private static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Every-screw hint failed (further errors suppressed): {e}");
    }
}

/// <summary>Taking apart: every screw still in that nothing holds down.</summary>
[HarmonyPatch]
internal static class EveryScrewDisassembleHintPatch
{
    private static bool Prepare() => EveryScrewHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.HintShowDisassembling)!;

    private static bool Prefix(Device targetDevice, TooltipProjectionHighlighter ___projectionHighlighter) =>
        EveryScrewHintFeature.RunGameHint(targetDevice, ___projectionHighlighter,
            socket => socket.IsBlocked ? null : socket.NestedElement);
}

/// <summary>Putting back: every empty screw slot the game would take a screw in now.</summary>
[HarmonyPatch]
internal static class EveryScrewAssembleHintPatch
{
    private static bool Prepare() => EveryScrewHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.HintShowAssembling)!;

    private static bool Prefix(Device targetDevice, TooltipProjectionHighlighter ___projectionHighlighter) =>
        EveryScrewHintFeature.RunGameHint(targetDevice, ___projectionHighlighter,
            socket => socket.IsAvailable ? socket.LastNestedElement : null);
}
