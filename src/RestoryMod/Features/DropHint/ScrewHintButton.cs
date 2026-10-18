using System;
using System.Reflection;
using BepInEx.Configuration;
using DG.Tweening;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using Rewired;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The gamepad's Select button (Share / Create on PlayStation, View / Back on Xbox) at the workbench
/// pulses the drop hint's yellow ghosts over every screw that can come out now. Holding it keeps them
/// at full brightness; letting go fades them as usual.
/// </summary>
internal static class ScrewHintButtonFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Controller", "SelectShowsScrewHints", true,
            "At the workbench, the gamepad's Select button (Share / Create, View / Back) pulses yellow over every " +
            "screw you can unscrew now. Hold it to keep them lit until you let go.");
    }

    public static bool TargetsPresent() => DropHintFeature.TargetsPresent() && GameTargets.AllPresent(
        PatchTargets.TooltipServiceInitialize,
        PatchTargets.TooltipServiceIsActive,
        PatchTargets.TooltipServiceDevices,
        PatchTargets.TooltipServiceStateMachine,
        PatchTargets.TooltipServiceSmallHandler,
        PatchTargets.DisassembleDetectionState,
        PatchTargets.StateMachineActiveState,
        PatchTargets.PlacedDevice,
        PatchTargets.DeviceContainerDevice,
        PatchTargets.DeviceElementSockets);

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Screw hint button failed (further errors suppressed): {e}");
    }
}

/// <summary>Keeps the game's tooltip service, which holds the workbench device and the highlighter.</summary>
[HarmonyPatch]
internal static class ScrewHintServicePatch
{
    public static DisassembleTooltipService? Service { get; private set; }

    private static bool Prepare() => ScrewHintButtonFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipServiceInitialize)!;

    private static void Postfix(DisassembleTooltipService __instance) => Service = __instance;
}

/// <summary>Reads Select each frame: a press ghosts the screws, holding keeps the pulse at its peak.</summary>
internal sealed class ScrewHintPoller : MonoBehaviour
{
    /// <summary>The game reads gamepad input as this Rewired player.</summary>
    private const int GamePlayerId = 0;

    private static readonly AccessTools.FieldRef<DisassembleTooltipService, DeviceService> Devices =
        AccessTools.FieldRefAccess<DisassembleTooltipService, DeviceService>("deviceService");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, DisassembleStateMachine> StateMachine =
        AccessTools.FieldRefAccess<DisassembleTooltipService, DisassembleStateMachine>("disassembleStateMachine");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, SmallElementTooltipHandler> SmallHandler =
        AccessTools.FieldRefAccess<DisassembleTooltipService, SmallElementTooltipHandler>("smallElementTooltipHandler");
    private static readonly AccessTools.FieldRef<SmallElementTooltipHandler, TooltipProjectionHighlighter> Highlighter =
        AccessTools.FieldRefAccess<SmallElementTooltipHandler, TooltipProjectionHighlighter>("tooltipProjectionHighlighter");

    /// <summary>The highlighter whose pulse Select started and still holds, null once let go.</summary>
    private TooltipProjectionHighlighter? holding;

    private void Update()
    {
        try
        {
            if (!ReInput.isReady) return;
            var player = ReInput.players.GetPlayer(GamePlayerId);
            if (player is null) return;
            SelectButton(player, out var pressed, out var down);

            if (pressed && ScrewHintButtonFeature.Enabled.Value) holding = ShowHints();
            if (!holding) return;

            var pulse = DropHintOneGhostPatch.Pulse(holding!);
            if (!down)
            {
                holding = null;
                if (pulse is not null && pulse.IsActive()) pulse.Play();
                return;
            }
            if (pulse is null || !pulse.IsActive()) return;
            var peak = PulseRewind.HoldAtPeak(pulse.Elapsed(false), DropHintOneGhostPatch.ToPeak(holding!));
            if (peak is not null) pulse.Goto(peak.Value, andPlay: false);
        }
        catch (Exception e)
        {
            ScrewHintButtonFeature.LogOnce(e);
        }
    }

    /// <summary>Select on any of the player's gamepads: pressed this frame, and held down now.</summary>
    private static void SelectButton(Player player, out bool pressed, out bool down)
    {
        pressed = down = false;
        foreach (var joystick in player.controllers.Joysticks)
        {
            var gamepad = joystick.GetTemplate<IGamepadTemplate>();
            if (gamepad is null) continue;
            pressed |= gamepad.center1.justPressed;
            down |= gamepad.center1.value;
        }
    }

    /// <summary>Ghosts every screw on the workbench device that can come out now; the highlighter, or null.</summary>
    private static TooltipProjectionHighlighter? ShowHints()
    {
        var service = ScrewHintServicePatch.Service;
        if (service is null || !service.IsActive) return null;
        var machine = StateMachine(service);
        if (!machine || machine.ActiveState is not DetectionDisassembleState) return null;
        var devices = Devices(service);
        var container = devices ? devices.PlacedDeviceContainer : null;
        var device = container ? container!.Device : null;
        var handler = SmallHandler(service);
        var highlighter = handler is null ? null : Highlighter(handler);
        if (!device || !highlighter) return null;

        var any = false;
        foreach (var socket in device!.ElementSockets)
        {
            var screw = socket.NestedElement;
            if (socket.CompatibleElementInfo.Category != ElementCategory.Small || !screw ||
                screw.Info.Category != ElementCategory.Small || socket.IsBlocked)
                continue;
            highlighter!.CreateAndHighlightProjection(screw.ProjectionData, socket.transform);
            any = true;
        }
        return any ? highlighter : null;
    }
}
