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
/// button; see <see cref="EveryPartHintFeature"/> for what it shows. While R2 keeps it lit (held, or
/// until pressed again, per the "R2 hint" setting) the pulse stays at full brightness and ready parts
/// are checked for again, so parts that become ready join it; once let go it fades as usual. R2
/// otherwise zooms out, which <see cref="ZoomToggle.ZoomToggleFeature"/> takes over.
/// </summary>
internal sealed class ScrewHintButtonFeature : IFeature
{
    public static ConfigEntry<HintButtonMode> Mode = null!;

    public FeatureManifest Manifest => FeatureManifests.ScrewHintButton;

    public void Load(FeatureContext ctx)
    {
        Mode = ctx.Config.Bind("Controller", "R2Hint", HintButtonMode.Hold,
            "At the workbench, R2 shows the hint, as Space does on a keyboard. Hold: it stays lit while R2 is " +
            "held. Toggle: it stays lit until R2 is pressed again or you leave the workbench. Off: R2 leaves " +
            "the hint alone. Also in the settings menu as \"R2 hint\". With TriggerZoomToggle off, R2 also still " +
            "zooms out.");
        if (TargetsPresent()) ctx.AddPoller<ScrewHintPoller>();
    }

    public static bool TargetsPresent() => HintPulse.TargetsPresent() && WorkbenchSession.TargetsPresent()
        && GameTargets.AllPresent(FeatureManifests.ScrewHintButton);

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Screw hint button failed (further errors suppressed): {e}");
    }
}

/// <summary>Reads R2 each frame: a press runs the game's hint, and while R2 keeps it lit the pulse stays at its peak.</summary>
internal sealed class ScrewHintPoller : MonoBehaviour
{
    private static readonly MethodInfo? GameHintPressed = GameTargets.Method(PatchTargets.TooltipServiceShow);
    private static readonly object[] NoEvent = { default(InputActionEventData) };

    /// <summary>How often a lit hint checks again for parts that became ready.</summary>
    private const float RecheckSeconds = 0.2f;

    /// <summary>The highlighter whose pulse R2 started and still holds, null once let go.</summary>
    private TooltipProjectionHighlighter? holding;

    private readonly HintButton button = new();

    private float nextRecheck;

    private void Update()
    {
        try
        {
            GamepadTriggers.Right(out var pressed, out var down);

            // Only a press at the bench itself counts, and only there does a lit hint check again: R2
            // works the tool while Cleaning or painting. The game drops its device on leaving the bench.
            var service = WorkbenchSession.Service;
            var atWorkbench = service is not null && (!WorkbenchSession.StatePresent || HasDevice(service));
            var detecting = service is not null && (!WorkbenchSession.StatePresent || WorkbenchSession.Detecting(service));
            if (button.Update(ScrewHintButtonFeature.Mode.Value, pressed && detecting, down, atWorkbench))
            {
                holding = RunGameHint();
                nextRecheck = Time.unscaledTime + RecheckSeconds;
            }
            else if (button.Lit && detecting && Time.unscaledTime >= nextRecheck)
            {
                nextRecheck = Time.unscaledTime + RecheckSeconds;
                var rechecked = Recheck();
                if (rechecked) holding = rechecked;
            }
            if (!holding) return;

            if (button.Lit)
            {
                HintPulse.HoldAtPeak(holding!);
                return;
            }
            // A toggled hint goes out on the press that turns it off; a held one fades as the game's would.
            if (ScrewHintButtonFeature.Mode.Value == HintButtonMode.Toggle) HintPulse.End(holding!);
            else HintPulse.Release(holding!);
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

    private static bool HasDevice(DisassembleTooltipService service)
    {
        var device = WorkbenchSession.Device(service);
        return device;
    }

    /// <summary>
    /// Runs the game's hint as its own button would (it checks for the workbench itself); the
    /// highlighter when the hint pulsed some screw, else null, so a pulse R2 didn't add to (a drop's)
    /// is never held.
    /// </summary>
    private static TooltipProjectionHighlighter? RunGameHint() =>
        GameHintPressed is null ? null : Pulsed((service, _) => GameHintPressed.Invoke(service, NoEvent));

    /// <summary>Checks again for ready parts while the hint is lit; the highlighter if that pulsed a new one.</summary>
    private static TooltipProjectionHighlighter? Recheck() =>
        EveryPartHintFeature.Enabled.Value ? Pulsed(EveryPartHintFeature.Refresh) : null;

    private static TooltipProjectionHighlighter? Pulsed(Action<DisassembleTooltipService, TooltipProjectionHighlighter> hint)
    {
        var service = WorkbenchSession.Service;
        if (service is null) return null;
        var highlighter = WorkbenchSession.Highlighter(service);
        if (!highlighter) return null;

        var shown = HintGhosts.For(highlighter!).ShowsAny(() => hint(service, highlighter!));
        return shown && HintPulse.Running(highlighter!) is not null ? highlighter : null;
    }
}
