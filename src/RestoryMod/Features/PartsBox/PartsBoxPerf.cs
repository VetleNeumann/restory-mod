using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.PartsBox;

/// <summary>[Debug] PartsBoxDiagnostics: parts box timing, and the missing-parts inputs in the log.</summary>
internal static class PartsBoxDiagnostics
{
    private static ConfigEntry<bool> entry = null!;

    public static bool Enabled => entry.Value;

    public static void Bind(ConfigFile config)
    {
        entry = config.Bind("Debug", "PartsBoxDiagnostics", false,
            "Log parts box timing (how much of opening it is this mod) and the missing-parts inputs. " +
            "Timing needs a game restart to switch on or off.");
    }
}

/// <summary>
/// Diagnostic timing for opening the parts box: total time in the game's InventoryPanel.Show, our
/// share of it broken down by step, and the frame times right after.
/// </summary>
internal static class PartsBoxPerf
{
    private static readonly Dictionary<string, Stopwatch> Steps = new();
    private static int framesToLog;
    private static int framesLogged;

    /// <summary>Times a step: <c>using (PartsBoxPerf.Measure("sort")) { ... }</c>. Nested steps double-count.</summary>
    public static Scope Measure(string step)
    {
        if (!PartsBoxDiagnostics.Enabled) return default;
        if (!Steps.TryGetValue(step, out var sw)) Steps[step] = sw = new Stopwatch();
        sw.Start();
        return new Scope(sw);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Stopwatch? sw;
        public Scope(Stopwatch sw) => this.sw = sw;
        public void Dispose() => sw?.Stop();
    }

    public static void ShowStarted() => ResetSteps();

    public static void ShowFinished(double totalMs) =>
        Report($"Parts box Show(): {totalMs:F1} ms total; ours by step: ", logFrames: true);

    /// <summary>Logs and resets the step timers (also used for the scene-load prewarm).</summary>
    public static void Report(string prefix, bool logFrames = false)
    {
        var steps = Steps.Where(kv => kv.Value.ElapsedTicks > 0)
            .Select(kv => $"{kv.Key} {kv.Value.Elapsed.TotalMilliseconds:F1} ms");
        Plugin.Logger.LogInfo(prefix + string.Join(", ", steps));
        ResetSteps();
        if (!logFrames) return;
        framesToLog = 5;
        framesLogged = 0;
    }

    /// <summary>Called every frame by the panel component; logs the frames following a Show().</summary>
    public static void Frame()
    {
        if (framesToLog <= 0) return;
        framesToLog--;
        framesLogged++;
        Plugin.Logger.LogInfo($"Parts box frame +{framesLogged}: {Time.unscaledDeltaTime * 1000f:F1} ms");
    }

    private static void ResetSteps()
    {
        foreach (var sw in Steps.Values) sw.Reset();
    }
}

/// <summary>Times InventoryPanel.Show. Only patched when diagnostics are on at startup.</summary>
[HarmonyPatch]
internal static class PartsBoxShowTimingPatch
{
    private static bool Prepare() =>
        PartsBoxDiagnostics.Enabled && GameTargets.AllPresent(PatchTargets.InventoryPanelShow);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.InventoryPanelShow)!;

    private static void Prefix(out Stopwatch __state)
    {
        PartsBoxPerf.ShowStarted();
        __state = Stopwatch.StartNew();
    }

    private static void Postfix(Stopwatch __state) => PartsBoxPerf.ShowFinished(__state.Elapsed.TotalMilliseconds);
}
