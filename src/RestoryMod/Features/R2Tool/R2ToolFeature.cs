using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.PlayerInput;
using RestoryMod.Core;
using RestoryMod.Features.DropHint;
using RestoryMod.Features.ShoulderButtons;
using RestoryMod.Features.ZoomToggle;
using Rewired;
using UnityEngine;

namespace RestoryMod.Features.R2Tool;

/// <summary>
/// While Cleaning or painting, R2 works the tool exactly as Cross (A on Xbox) does: its press and
/// release run the state's own handlers for Cross, so finishing a part or stopping a solder behaves the
/// same. The two count as one button, released once both are up. Only while TriggerZoomToggle is on:
/// with it off the triggers zoom there, as in the game.
/// </summary>
internal sealed class R2ToolFeature : IFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public FeatureManifest Manifest => FeatureManifests.R2Tool;

    public void Load(FeatureContext ctx)
    {
        Enabled = ctx.Config.Bind("Controller", "R2Cleans", true,
            "While cleaning, soldering or painting a part, R2 works the tool as Cross (A on Xbox) does. Only " +
            "while TriggerZoomToggle is on; with it off, R2 zooms there.");
        ctx.Patch(typeof(R2ToolCrossPressPatch), typeof(R2ToolCrossReleasePatch), typeof(R2ToolPaintingUpdatePatch),
            typeof(R2ToolPaintingButtonPatch));
        if (TargetsPresent()) ctx.AddPoller<R2ToolPoller>();
    }

    public static bool TargetsPresent() => WorkbenchSession.TargetsPresent() && GameTargets.AllPresent(FeatureManifests.R2Tool);

    /// <summary>R2 takes the tool now: switched on, and the triggers aren't zooming.</summary>
    public static bool Active => Enabled.Value && ZoomToggleFeature.Enabled.Value && ZoomToggleFeature.TargetsPresent();

    private static readonly EitherButton Execute = new();
    private static object? executeState;

    /// <summary>
    /// Called each frame with the Cleaning or painting state the player is in, or null. Entering one
    /// starts with both buttons up: the game builds each state once and reuses it, and on leaving it
    /// stops listening, so a release that ended it is never seen.
    /// </summary>
    internal static void Track(object? state)
    {
        if (state == executeState) return;
        Execute.Reset();
        executeState = state;
    }

    /// <summary>Cross and R2 as one button in <paramref name="state"/>.</summary>
    internal static EitherButton For(object state)
    {
        Track(state);
        return Execute;
    }

    /// <summary>True while the mod runs a state's Cross handler for R2, so the Cross gate lets it through.</summary>
    internal static bool Forwarding;

    /// <summary>R2 is held as Cross, for painting's per-frame read of Cross.</summary>
    internal static bool R2Held;

    internal static bool InPaintingUpdate;

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"R2 tool failed (further errors suppressed): {e}");
    }
}

/// <summary>Reads R2 each frame and hands its press and release to the Cleaning or painting state.</summary>
internal sealed class R2ToolPoller : MonoBehaviour
{
    private static readonly MethodInfo? CleaningPressed = GameTargets.Method(PatchTargets.CleaningExecutePressed);
    private static readonly MethodInfo? CleaningReleased = GameTargets.Method(PatchTargets.CleaningExecuteReleased);
    private static readonly MethodInfo? PaintingPressed = GameTargets.Method(PatchTargets.PaintingExecutePressed);
    private static readonly MethodInfo? PaintingReleased = GameTargets.Method(PatchTargets.PaintingExecuteReleased);
    private static readonly object[] NoEvent = { default(InputActionEventData) };

    /// <summary>The state R2 went down in while it works the tool there, else null.</summary>
    private object? pressedIn;

    private void Update()
    {
        try
        {
            GamepadTriggers.Right(out var pressed, out var down);
            var state = ToolState();
            if (pressedIn is not null && state != pressedIn)
            {
                // Leaving the state ends the press on its own: the game clears it on exit.
                pressedIn = null;
                R2ToolFeature.R2Held = false;
            }
            R2ToolFeature.Track(state);

            if (pressedIn is not null && !down)
            {
                if (R2ToolFeature.For(state!).Release(second: true)) Run(state!, released: true);
                pressedIn = null;
                R2ToolFeature.R2Held = false;
            }

            if (!pressed || state is null || !R2ToolFeature.Active || ShoulderButtonsFeature.IsPaused()) return;
            pressedIn = state;
            R2ToolFeature.R2Held = true;
            if (R2ToolFeature.For(state).Press(second: true)) Run(state, released: false);
        }
        catch (Exception e)
        {
            R2ToolFeature.LogOnce(e);
        }
    }

    /// <summary>Unloaded while R2 is held (a hot reload): lets go, as releasing R2 would.</summary>
    private void OnDestroy()
    {
        try
        {
            var state = ToolState();
            if (pressedIn is not null && state == pressedIn && R2ToolFeature.For(state).Release(second: true))
                Run(state, released: true);
        }
        catch (Exception e)
        {
            R2ToolFeature.LogOnce(e);
        }
        pressedIn = null;
        R2ToolFeature.R2Held = false;
    }

    /// <summary>The Cleaning or painting state while the player is in it, else null.</summary>
    private static object? ToolState()
    {
        var service = WorkbenchSession.Service;
        if (service is null || !service.IsActive) return null;
        var state = WorkbenchSession.ActiveState(service);
        return state is CleaningDisassembleState or PaintingDisassembleState ? state : null;
    }

    private static void Run(object state, bool released)
    {
        var handler = state is CleaningDisassembleState
            ? released ? CleaningReleased : CleaningPressed
            : released ? PaintingReleased : PaintingPressed;
        if (handler is null) return;
        R2ToolFeature.Forwarding = true;
        try
        {
            handler.Invoke(state, NoEvent);
        }
        finally
        {
            R2ToolFeature.Forwarding = false;
        }
    }
}

/// <summary>A Cross press while R2 already works the tool doesn't press again.</summary>
[HarmonyPatch]
internal static class R2ToolCrossPressPatch
{
    private static bool Prepare() => R2ToolFeature.TargetsPresent();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return GameTargets.Method(PatchTargets.CleaningExecutePressed)!;
        yield return GameTargets.Method(PatchTargets.PaintingExecutePressed)!;
    }

    private static bool Prefix(object __instance)
    {
        if (R2ToolFeature.Forwarding) return true;
        try
        {
            return R2ToolFeature.For(__instance).Press(second: false);
        }
        catch (Exception e)
        {
            R2ToolFeature.LogOnce(e);
            return true;
        }
    }
}

/// <summary>Letting go of Cross while R2 is still held keeps the tool going.</summary>
[HarmonyPatch]
internal static class R2ToolCrossReleasePatch
{
    private static bool Prepare() => R2ToolFeature.TargetsPresent();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return GameTargets.Method(PatchTargets.CleaningExecuteReleased)!;
        yield return GameTargets.Method(PatchTargets.PaintingExecuteReleased)!;
    }

    private static bool Prefix(object __instance)
    {
        if (R2ToolFeature.Forwarding) return true;
        try
        {
            return R2ToolFeature.For(__instance).Release(second: false);
        }
        catch (Exception e)
        {
            R2ToolFeature.LogOnce(e);
            return true;
        }
    }
}

/// <summary>Marks painting's per-frame update, where the brush reads Cross as held each frame.</summary>
[HarmonyPatch]
internal static class R2ToolPaintingUpdatePatch
{
    private static bool Prepare() => R2ToolFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.PaintingOnUpdate)!;

    private static void Prefix() => R2ToolFeature.InPaintingUpdate = true;

    private static void Postfix() => R2ToolFeature.InPaintingUpdate = false;
}

/// <summary>Inside painting's update, Cross reads as held while R2 is.</summary>
[HarmonyPatch]
internal static class R2ToolPaintingButtonPatch
{
    private static MethodBase? target;

    private static bool Prepare()
    {
        if (!R2ToolFeature.TargetsPresent()) return false;
        target ??= AccessTools.Method(typeof(RewiredPlayerInput), PatchTargets.PlayerInputGetButtonRaw.MemberName, [typeof(int)]);
        if (target is null) Plugin.Logger.LogWarning($"Patch target missing: {PatchTargets.PlayerInputGetButtonRaw} (int overload)");
        return target is not null;
    }

    private static MethodBase TargetMethod() => target!;

    private static void Postfix(int actionId, ref bool __result)
    {
        if (R2ToolFeature.InPaintingUpdate && R2ToolFeature.R2Held && actionId == RewiredConsts.Action.MiniGame.Activate)
            __result = true;
    }
}
