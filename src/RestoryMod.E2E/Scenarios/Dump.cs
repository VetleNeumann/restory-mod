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
    }

    private IEnumerable<string> List(string key) =>
        Options.TryGetValue(key, out var value) ? value.Split(',').Select(p => p.Trim()).Where(p => p != "") : Enumerable.Empty<string>();

    private static void Log(string when)
    {
        E2EPlugin.Log($"[E2E] marks {when}:");
        foreach (var line in Bench.Describe(Bench.Marks(Bench.Things()))) E2EPlugin.Log(line);
    }
}
