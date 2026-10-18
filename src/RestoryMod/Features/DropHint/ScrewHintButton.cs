using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using Rewired;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The gamepad's Select button (Share / Create on PlayStation, View / Back on Xbox) at the workbench
/// runs the game's own hint, whose GetTooltip action (Space) has no gamepad button; see
/// <see cref="EveryScrewHintFeature"/> for what it shows. Holding it keeps the pulse at full
/// brightness; letting go fades it as usual.
/// </summary>
internal static class ScrewHintButtonFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Controller", "SelectShowsScrewHints", true,
            "At the workbench, the gamepad's Select button (Share / Create, View / Back) shows the game's screw " +
            "hint, as Space does on a keyboard. Hold it to keep the hint lit until you let go.");
    }

    public static bool TargetsPresent() => HintPulse.TargetsPresent() && GameTargets.AllPresent(
        PatchTargets.TooltipServiceInitialize,
        PatchTargets.TooltipServiceDispose,
        PatchTargets.TooltipServiceShow,
        PatchTargets.TooltipServiceSmallHandler,
        PatchTargets.DropHintHighlighter);

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Screw hint button failed (further errors suppressed): {e}");
    }
}

/// <summary>Keeps the game's tooltip service, which runs the hint and holds its highlighter.</summary>
[HarmonyPatch]
internal static class ScrewHintServicePatch
{
    public static DisassembleTooltipService? Service { get; set; }

    private static bool Prepare() => ScrewHintButtonFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipServiceInitialize)!;

    private static void Postfix(DisassembleTooltipService __instance) => Service = __instance;
}

/// <summary>Forgets the tooltip service when its scene unloads.</summary>
[HarmonyPatch]
internal static class ScrewHintServiceDisposePatch
{
    private static bool Prepare() => ScrewHintButtonFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipServiceDispose)!;

    private static void Postfix(DisassembleTooltipService __instance)
    {
        if (ScrewHintServicePatch.Service == __instance) ScrewHintServicePatch.Service = null;
    }
}

/// <summary>Reads Select each frame: a press runs the game's hint, holding keeps the pulse at its peak.</summary>
internal sealed class ScrewHintPoller : MonoBehaviour
{
    /// <summary>The game reads gamepad input as this Rewired player.</summary>
    private const int GamePlayerId = 0;

    private static readonly MethodInfo? GameHintPressed = GameTargets.Method(PatchTargets.TooltipServiceShow);
    private static readonly object[] NoEvent = { default(InputActionEventData) };

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

            if (pressed && ScrewHintButtonFeature.Enabled.Value) holding = RunGameHint();
            if (!holding) return;

            if (down)
            {
                HintPulse.HoldAtPeak(holding!);
                return;
            }
            HintPulse.Release(holding!);
            holding = null;
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

    /// <summary>
    /// Runs the game's hint as its own button would (it checks for the workbench itself); the
    /// highlighter when the hint pulsed some screw, else null, so a pulse Select didn't add to (a drop's)
    /// is never held.
    /// </summary>
    private static TooltipProjectionHighlighter? RunGameHint()
    {
        var service = ScrewHintServicePatch.Service;
        if (service is null || GameHintPressed is null) return null;
        var highlighter = TooltipServiceParts.Highlighter(service);
        if (!highlighter) return null;

        var before = HintPulse.Requests;
        GameHintPressed.Invoke(service, NoEvent);
        return HintPulse.Requests != before && HintPulse.Running(highlighter!) is not null ? highlighter : null;
    }
}
