using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.PartsBox;

/// <summary>
/// Diagnostic timing for opening the parts box: total time in the game's InventoryPanel.Show, our
/// share of it broken down by step, and the frame times right after. Toggle with [Debug] PartsBoxTiming.
/// </summary>
internal static class PartsBoxPerf
{
    private static readonly Dictionary<string, Stopwatch> Steps = new();
    private static int framesToLog;
    private static int framesLogged;

    public static bool Enabled => DebugTiming.Enabled.Value;

    /// <summary>Times a step: <c>using (PartsBoxPerf.Measure("sort")) { ... }</c>. Nested steps double-count.</summary>
    public static Scope Measure(string step)
    {
        if (!Enabled) return default;
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

    public static void ShowStarted()
    {
        foreach (var sw in Steps.Values) sw.Reset();
    }

    public static void ShowFinished(double totalMs) =>
        Report($"Parts box Show(): {totalMs:F1} ms total; ours by step: ", logFrames: true);

    /// <summary>Logs and resets the step timers (also used for the scene-load prewarm).</summary>
    public static void Report(string prefix, bool logFrames = false)
    {
        var steps = Steps.Where(kv => kv.Value.ElapsedTicks > 0)
            .Select(kv => $"{kv.Key} {kv.Value.Elapsed.TotalMilliseconds:F1} ms");
        Plugin.Logger.LogInfo(prefix + string.Join(", ", steps));
        foreach (var sw in Steps.Values) sw.Reset();
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
}

internal static class DebugTiming
{
    public static BepInEx.Configuration.ConfigEntry<bool> Enabled = null!;

    public static void Bind(BepInEx.Configuration.ConfigFile config)
    {
        Enabled = config.Bind("Debug", "PartsBoxDiagnostics", false,
            "Log parts box timing (how much of opening it is this mod) and the missing-parts inputs.");
    }
}

[HarmonyPatch]
internal static class PartsBoxShowTimingPatch
{
    private static bool Prepare() => GameTargets.Method(PatchTargets.InventoryPanelShow) is not null;

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.InventoryPanelShow)!;

    private static void Prefix(out Stopwatch? __state)
    {
        __state = null;
        if (!PartsBoxPerf.Enabled) return;
        PartsBoxPerf.ShowStarted();
        __state = Stopwatch.StartNew();
    }

    private static void Postfix(Stopwatch? __state)
    {
        if (__state == null) return;
        PartsBoxPerf.ShowFinished(__state.Elapsed.TotalMilliseconds);
    }
}
