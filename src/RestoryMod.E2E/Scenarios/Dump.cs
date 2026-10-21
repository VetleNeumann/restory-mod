using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.E2E.Scenarios;

/// <summary>
/// Not a test: logs the device models, then (with model=...) puts one on the bench and logs every thing
/// there with its mark, at rest and while R2 shows the hint. For writing scenarios against real names.
/// Options: model=, dirty=part,part, damaged=part,part.
/// </summary>
internal sealed class Dump : IScenario
{
    public Dictionary<string, string> Options { private get; set; } = new();

    public IEnumerator Run(Driver d)
    {
        d.Setting("Controller", "R2Hint", Core.HintButtonMode.Hold);
        yield return Game.LoadSave();
        foreach (var line in Game.DescribeModels()) E2EPlugin.Log("[E2E] model " + line);
        if (!Options.TryGetValue("model", out var model)) yield break;

        var conditions = new Dictionary<string, Condition>();
        foreach (var part in List("dirty")) conditions[part] = Condition.Dirty;
        foreach (var part in List("damaged")) conditions[part] = Condition.Damaged;
        yield return Game.PlaceDevice(Game.Model(model), conditions);
        yield return Game.EnterWorkbench();
        Log("at rest");
        yield return d.R2(true);
        yield return Driver.Wait(0.5f);
        Log("R2 held");
        yield return d.R2(false);
        yield return Driver.Wait(1f);
        Log("R2 let go");
        if (Options.ContainsKey("apart")) yield return Apart(d);
    }

    // Follows the hint: takes off the first attached thing it pulses, logs, and again until none pulses.
    private IEnumerator Apart(Driver d)
    {
        for (var step = 0; step < 60; step++)
        {
            yield return d.R2(true);
            yield return Driver.Wait(0.5f);
            var things = Bench.Things();
            var marks = Bench.Marks(things);
            yield return d.R2(false);
            var next = things.FirstOrDefault(t => t.Socket && t.Part && marks[t.Name] == Mark.Pulse);
            if (next is null)
            {
                Log($"step {step}: no attached part pulses");
                yield break;
            }
            E2EPlugin.Log($"[E2E] step {step}: taking off {next.Name}");
            var drop = Driver.OnScreen(0.25f + 0.1f * (step % 6), step % 2 == 0 ? 0.2f : 0.28f);
            yield return d.TakeOff(next.Name, drop);
            yield return d.Park();
            yield return d.R2(true);
            yield return Driver.Wait(0.5f);
            Log($"after step {step}, R2 held");
            yield return d.R2(false);
        }
    }

    private IEnumerable<string> List(string key) =>
        Options.TryGetValue(key, out var value) ? value.Split(',').Select(p => p.Trim()).Where(p => p != "") : Enumerable.Empty<string>();

    private static void Log(string when)
    {
        E2EPlugin.Log($"[E2E] marks {when}:");
        var things = Bench.Things();
        foreach (var line in Bench.Describe(Bench.Marks(things))) E2EPlugin.Log(line);
        foreach (var screw in Bench.Hidden(things))
            E2EPlugin.Log($"[E2E]     {screw.Key}: {(screw.Value ? "hidden" : "in view")}");
    }
}
