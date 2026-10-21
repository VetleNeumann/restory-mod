using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RestoryMod.E2E.Scenarios;
using Rewired;
using UnityEngine;

namespace RestoryMod.E2E;

/// <summary>
/// The end-to-end harness. Only scripts/dev.sh e2e deploys it, with a request file naming the scenario
/// to run; without one it does nothing. The run's report goes to the log and to e2e/result.txt.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.zantox.restorymod")]
public class E2EPlugin : BaseUnityPlugin
{
    private static ManualLogSource logger = null!;

    private static readonly Dictionary<string, Func<IScenario>> Scenarios = new()
    {
        ["dump"] = () => new Dump(),
        ["full-repair-r2-hold"] = () => new FullRepairR2Hold(),
    };

    private string outDir = "";

    private static string logPath = "";

    /// <summary>
    /// Logs a line, and writes it straight to e2e/log.txt too: BepInEx buffers its own log file, so
    /// scripts/dev.sh would miss the last lines of a run there.
    /// </summary>
    internal static void Log(string line)
    {
        logger.LogInfo(line);
        File.AppendAllText(logPath, line + "\n");
    }

    private void Awake()
    {
        logger = Logger;
        outDir = Path.Combine(Paths.BepInExRootPath, "e2e");
        Directory.CreateDirectory(outDir);
        logPath = Path.Combine(outDir, "log.txt");
        var requestPath = Path.Combine(outDir, "request.txt");
        if (!File.Exists(requestPath))
        {
            Log("[E2E] no request: idle");
            return;
        }
        var request = File.ReadAllLines(requestPath)
            .Select(l => l.Split(new[] { '=' }, 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        File.Delete(requestPath);
        File.Delete(Path.Combine(outDir, "result.txt"));

        var name = request.TryGetValue("scenario", out var s) ? s : "";
        int? stopAt = request.TryGetValue("stopAt", out var n) && int.TryParse(n, out var at) ? at : null;
        if (!Scenarios.TryGetValue(name, out var make))
        {
            Finish(new[] { $"[E2E] RESULT ABORTED after #0: no scenario '{name}'; scenarios: {string.Join(", ", Scenarios.Keys)}" });
            return;
        }
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        if (!SaveBlock.Apply(harmony))
        {
            Finish(new[] { "[E2E] RESULT ABORTED after #0: the game's save writes moved, so saves can't be blocked" });
            return;
        }
        try
        {
            SeeThrough.Apply(harmony);
        }
        catch (StepFailed e)
        {
            Finish(new[] { $"[E2E] RESULT ABORTED after #0: {e.Message}" });
            return;
        }
        Log($"[E2E] running {name}{(stopAt is { } stop ? $", stopping before #{stop}" : "")}; saves blocked");
        var scenario = make();
        scenario.Options = request;
        StartCoroutine(Run(scenario, new RunLedger(stopAt)));
    }

    private IEnumerator Run(IScenario scenario, RunLedger ledger)
    {
        // Rewired starts up after the plugins load.
        while (!ReInput.isReady) yield return null;
        VirtualPad pad;
        List<Joystick> before;
        try
        {
            pad = VirtualPad.Connect(out before);
        }
        catch (Exception e)
        {
            ledger.Abort($"no virtual pad (is ViGEmBus installed?): {e.Message}");
            Finish(ledger.Report());
            yield break;
        }
        var driver = new Driver(pad, ledger, outDir);
        var stack = new Stack<IEnumerator>();
        stack.Push(scenario.Run(driver));
        // Runs first, inside the same failure handling as the scenario's steps.
        stack.Push(Game.Until(() => pad.TakeOver(before), 10f, "Rewired saw the virtual pad"));
        var stopped = false;
        while (stack.Count > 0)
        {
            var top = stack.Peek();
            bool moved;
            try
            {
                moved = top.MoveNext();
            }
            catch (StopHere)
            {
                stopped = true;
                break;
            }
            catch (Exception e)
            {
                var shot = Path.Combine(outDir, "aborted.png");
                ScreenCapture.CaptureScreenshot(shot);
                ledger.Abort((e is StepFailed ? e.Message : e.ToString()) + $"; screenshot: {shot}");
                break;
            }
            if (!moved)
            {
                stack.Pop();
                continue;
            }
            if (top.Current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            if (pad.AbortPressed())
            {
                ledger.Abort("aborted with Start on the real pad");
                break;
            }
            yield return top.Current;
        }
        pad.Release();
        // However the run ended, the player's own cursor sees only what's in front.
        SeeThrough.Target = null;
        if (stopped)
        {
            // Left as it stands for the player to look around, with their own pad back. The scenario's
            // settings stay, so what they see is what the next check would have seen.
            pad.HandBack();
        }
        else
        {
            if (ledger.Verdict is RunVerdict.Passed or RunVerdict.Failed) ledger.Finish();
            driver.RestoreSettings();
            pad.Dispose();
        }
        Finish(ledger.Report(), alreadyLogged: driver.Logged);
    }

    /// <summary>Logs the report lines not logged yet and writes the whole report for scripts/dev.sh.</summary>
    private void Finish(IReadOnlyList<string> report, int alreadyLogged = 0)
    {
        foreach (var line in report.Skip(alreadyLogged)) Log(line);
        Directory.CreateDirectory(outDir);
        File.WriteAllLines(Path.Combine(outDir, "result.txt"), report);
    }
}
