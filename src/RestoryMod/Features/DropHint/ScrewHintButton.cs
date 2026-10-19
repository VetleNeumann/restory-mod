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
/// R2 at the workbench runs the game's own hint, whose GetTooltip action (Space) has no gamepad
/// button; see <see cref="EveryPartHintFeature"/> for what it shows. Holding it keeps the pulse at
/// full brightness and keeps checking for ready parts, so parts that become ready join it; letting go
/// fades it as usual. R2 otherwise zooms out, which
/// <see cref="ZoomToggle.ZoomToggleFeature"/> takes over.
/// </summary>
internal static class ScrewHintButtonFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Controller", "R2ShowsScrewHints", true,
            "At the workbench, R2 shows the game's screw hint, as Space does on a keyboard. Hold it to keep the " +
            "hint lit until you let go. With TriggerZoomToggle off, R2 also still zooms out.");
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

    /// <summary>On unload: leaves the service for the next copy, which won't see Initialize again this scene.</summary>
    internal static void Unload()
    {
        if (Service is not null) Handoff.Leave(nameof(ScrewHintServicePatch), Service);
    }

    /// <summary>Picks up the service the previous copy kept on a hot reload; nothing on a normal start.</summary>
    internal static void Resume()
    {
        if (!Prepare() || Handoff.Take(nameof(ScrewHintServicePatch)) is not [DisassembleTooltipService service]) return;
        Service = service;
        Plugin.Logger.LogInfo("Screw hint service taken over from the previous plugin copy.");
    }
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

/// <summary>Reads R2 each frame: a press runs the game's hint, holding keeps the pulse at its peak.</summary>
internal sealed class ScrewHintPoller : MonoBehaviour
{
    private static readonly MethodInfo? GameHintPressed = GameTargets.Method(PatchTargets.TooltipServiceShow);
    private static readonly object[] NoEvent = { default(InputActionEventData) };

    /// <summary>How often a held R2 checks again for parts that became ready.</summary>
    private const float RecheckSeconds = 0.2f;

    /// <summary>The highlighter whose pulse R2 started and still holds, null once let go.</summary>
    private TooltipProjectionHighlighter? holding;

    private float nextRecheck;

    private void Update()
    {
        try
        {
            GamepadTriggers.Right(out var pressed, out var down);

            if (pressed && ScrewHintButtonFeature.Enabled.Value)
            {
                holding = RunGameHint();
                nextRecheck = Time.unscaledTime + RecheckSeconds;
            }
            else if (down && ScrewHintButtonFeature.Enabled.Value && Time.unscaledTime >= nextRecheck)
            {
                nextRecheck = Time.unscaledTime + RecheckSeconds;
                var rechecked = Recheck();
                if (rechecked) holding = rechecked;
            }
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

    /// <summary>Unloaded while R2 holds the pulse (a hot reload): lets it fade as letting go of R2 would.</summary>
    private void OnDestroy()
    {
        if (!holding) return;
        try
        {
            HintPulse.Release(holding!);
        }
        catch (Exception e)
        {
            ScrewHintButtonFeature.LogOnce(e);
        }
    }

    /// <summary>
    /// Runs the game's hint as its own button would (it checks for the workbench itself); the
    /// highlighter when the hint pulsed some screw, else null, so a pulse R2 didn't add to (a drop's)
    /// is never held.
    /// </summary>
    private static TooltipProjectionHighlighter? RunGameHint() =>
        GameHintPressed is null ? null : Pulsed((service, _) => GameHintPressed.Invoke(service, NoEvent));

    /// <summary>Checks again for ready parts while R2 is held; the highlighter if that pulsed a new one.</summary>
    private static TooltipProjectionHighlighter? Recheck() =>
        EveryPartHintFeature.Enabled.Value ? Pulsed(EveryPartHintFeature.Refresh) : null;

    private static TooltipProjectionHighlighter? Pulsed(Action<DisassembleTooltipService, TooltipProjectionHighlighter> hint)
    {
        var service = ScrewHintServicePatch.Service;
        if (service is null) return null;
        var highlighter = TooltipServiceParts.Highlighter(service);
        if (!highlighter) return null;

        var before = HintPulse.Requests;
        hint(service, highlighter!);
        return HintPulse.Requests != before && HintPulse.Running(highlighter!) is not null ? highlighter : null;
    }
}
