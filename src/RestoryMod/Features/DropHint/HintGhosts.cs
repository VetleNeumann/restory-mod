using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The ghost ledger for the game's hint pulse: every ghost joins the pulse through it. The hint's own
/// ghosts leave as soon as their part stops being ready (picked up, put in, its socket filled or
/// blocked), are filled in rather than only outlined, and pulse red in missing sockets. They share the
/// game's pulse, so it still sets their timing.
/// </summary>
internal static class HintGhosts
{
    /// <summary>How often the hint checks whether its ghosts are still wanted.</summary>
    private const float RecheckSeconds = 0.2f;

    private static GhostLedger<Transform, ElementProjection>? ledger;
    private static TooltipProjectionHighlighter? owner;
    private static float nextRecheck;

    /// <summary>The ledger for this highlighter's pulse (the game has one).</summary>
    public static GhostLedger<Transform, ElementProjection> For(TooltipProjectionHighlighter highlighter)
    {
        if (ledger is not null && owner == highlighter) return ledger;
        Release();
        ledger = new GhostLedger<Transform, ElementProjection>(new GamePulse(highlighter), () => HintPulse.Tidy.Value);
        owner = highlighter;
        return ledger;
    }

    /// <summary>Every frame: keeps the ghosts in step with the pulse, and now and then with the plan.</summary>
    public static void Tick()
    {
        if (ledger is null) return;
        ledger.Tick();
        if (!ledger.Any || Time.unscaledTime < nextRecheck) return;
        nextRecheck = Time.unscaledTime + RecheckSeconds;
        Recheck();
    }

    /// <summary>Drops the hint's ghosts that a fresh plan no longer wants.</summary>
    public static void Recheck()
    {
        if (ledger is not { Any: true }) return;
        List<HintItem<Transform>>? plan;
        try
        {
            plan = EveryPartHintFeature.PlanNow();
        }
        catch (Exception e)
        {
            // The device went half away: its ghosts can't be checked, so they go, as a failed check did.
            EveryPartHintFeature.LogOnce(e);
            plan = new List<HintItem<Transform>>();
        }
        if (plan is not null) ledger.Reconcile(plan);
    }

    /// <summary>A part went in or out of this socket: its ghosts leave, and the others are checked again.</summary>
    public static void SocketChanged(TooltipProjectionHighlighter highlighter, Transform socket)
    {
        if (HintPulse.Tidy.Value) For(highlighter).Drop(socket);
        Recheck();
    }

    public static void PulseEnding(TooltipProjectionHighlighter highlighter)
    {
        if (ledger is not null && owner == highlighter) ledger.PulseEnded();
    }

    /// <summary>
    /// After a hot reload, while a pulse still runs: takes over the ghosts the old copy handed back
    /// plain, so they get the hint's look again and still leave once their part stops being ready.
    /// Needs the workbench session resumed first; nothing on a normal start.
    /// </summary>
    internal static void Resume()
    {
        var service = WorkbenchSession.Service;
        if (service is null || !EveryPartHintFeature.TargetsPresent()) return;
        var highlighter = WorkbenchSession.Highlighter(service);
        if (!highlighter || HintPulse.Running(highlighter!) is null) return;
        var plan = EveryPartHintFeature.PlanNow();
        if (plan is null) return;
        For(highlighter!).Adopt(plan);
        Plugin.Logger.LogInfo("Hint ghosts taken over from the previous plugin copy.");
    }

    /// <summary>On unload: hands every ghost back to the plain pulse and drops the copied presets.</summary>
    internal static void Unload()
    {
        Release();
        GamePulse.DestroyPresets();
        EveryPartHintFeature.Unload();
    }

    private static void Release()
    {
        try
        {
            ledger?.Unload();
        }
        catch (Exception e)
        {
            HintPulse.LogOnce(e); // the old pulse went with its scene
        }
        ledger = null;
        owner = null;
    }
}

/// <summary>Runs the hint ghosts' per-frame check.</summary>
internal sealed class HintGhostWatcher : MonoBehaviour
{
    private void Update()
    {
        try
        {
            HintGhosts.Tick();
        }
        catch (Exception e)
        {
            EveryPartHintFeature.LogOnce(e);
        }
    }
}

/// <summary>The pulse ending: red ghosts turn back to yellow before the game pools them.</summary>
[HarmonyPatch]
internal static class HintGhostsPulseEndPatch
{
    private static bool Prepare() => EveryPartHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.TooltipHighlighterComplete)!;

    private static void Prefix(TooltipProjectionHighlighter __instance) => HintGhosts.PulseEnding(__instance);
}
