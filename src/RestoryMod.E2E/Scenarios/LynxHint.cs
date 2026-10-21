using System.Collections;
using System.Collections.Generic;
using RestoryMod.Features.DropHint;
using UnityEngine;
using static RestoryMod.E2E.Mark;

namespace RestoryMod.E2E.Scenarios;

/// <summary>
/// The highlight rework on an Atari Lynx, R2 hint on Toggle: a blocked part stays dark under the cursor;
/// screws under the device are drawn hidden and screws in plain view are not; taking a battery out of
/// its tube keeps the hint on taking apart (the next battery sliding forward once read as one put in);
/// a battery socket nothing on the bench can fill shows the missing look until a spare arrives.
/// Every part starts unknown, so the hint asks for everything that can come out.
/// </summary>
internal sealed class LynxHint : IScenario
{
    private const string Model = "Atari_Lynx";
    private const string P = "Atari_Lynx_";
    private const string Cap = P + "BattaryCap";
    private const string BackCase = P + "BackCase";
    private const string FrontCase = P + "FrontCase";
    private const string CartridgeCap = P + "CartridgeCap";
    private const string Battery = P + "Battery_1";

    // The two battery tubes' entries: a battery comes out of and goes into a tube only there.
    private const string TubeA = Battery + " #1";
    private const string TubeB = Battery + " #4";

    private static string BackScrew(int n) => $"Atari_lynx_Screw_1 #{n}";
    private static string InnerScrew(int n) => $"Atari_lynx_Screw_5 #{n}";
    private static string Loose(string part) => "loose " + part;

    public Dictionary<string, string> Options { private get; set; } = new();

    private Driver d = null!;
    private int drops;

    public IEnumerator Run(Driver driver)
    {
        d = driver;
        d.Setting("Controller", "R2Hint", Core.HintButtonMode.Toggle);
        d.Setting("Workbench", "HintShowsEveryPart", true);
        yield return Game.LoadSave();
        yield return Game.PlaceDevice(Game.Model(Model), new Dictionary<string, Condition>());
        yield return Game.EnterWorkbench();
        yield return d.Park();
        yield return d.ExpectStays("nothing marked before R2", 1f);

        // Blocked hover: the front case is screwed down, so a press does nothing to it.
        yield return d.MoveTo(FrontCase);
        yield return d.ExpectIncludes("the screwed-down front case stays dark under the cursor", (FrontCase, None));
        yield return d.MoveTo(Cap);
        yield return d.ExpectIncludes("the battery cap, free to come off, lights up under the cursor", (Cap, HoverOutline));
        yield return d.Park();

        var backScrews = new[] { (BackScrew(1), Pulse), (BackScrew(2), Pulse), (BackScrew(3), Pulse), (BackScrew(4), Pulse) };
        yield return Toggle();
        yield return d.Expect("R2: the battery cap and the four back screws pulse", With(backScrews, (Cap, Pulse)));
        yield return d.ExpectHidden("the back screws lie under the device: drawn hidden",
            (BackScrew(1), true), (BackScrew(2), true), (BackScrew(3), true), (BackScrew(4), true));
        yield return Toggle();
        yield return d.ExpectWithin(0.5f, "R2 again: the hint goes at once");

        yield return d.TakeOff(Cap, Drop());
        yield return d.Park();
        var apart = With(backScrews, (TubeA, Pulse), (TubeB, Pulse));
        yield return Hint("cap off: both battery tubes and the back screws pulse", apart);

        // The next battery slides forward into the tube's entry, which the game takes for one put in.
        yield return d.TakeOff(TubeA, Drop());
        yield return d.Park();
        yield return Hint("a battery out: still taking apart, nothing to put back yet", apart);
        yield return d.TakeOff(TubeA, Drop());
        yield return d.Park();
        yield return Hint("a second battery out: still taking apart", apart);

        // Not under test: one battery thrown away by the game itself, then the other put back, which
        // switches the game to putting back with one battery short.
        Game.Discard(d.Thing(Loose(Battery) + " #2").Loose!);
        yield return Driver.Wait(0.5f);
        yield return d.PutBack(Loose(Battery), TubeA);
        yield return d.Park();
        yield return Hint("one battery short: its tube shows the missing look", (TubeA, MissingGhost));

        yield return Game.SpawnSpare(Game.Element(Model, Battery));
        yield return Hint("a spare battery on the bench: it pulses and the missing look goes", (Loose(Battery), Pulse));
        yield return d.PutBack(Loose(Battery), TubeA);
        yield return d.Park();
        yield return Hint("tube full: the cap pulses", (Loose(Cap), Pulse));
        yield return d.PutBack(Loose(Cap), Cap);
        yield return d.Park();

        // Back together, every part still unknown: the hint asks for the lot again.
        yield return Hint("whole again: the cap and the back screws pulse", With(backScrews, (Cap, Pulse)));
        yield return d.TakeOff(Cap, Drop());
        for (var i = 0; i < 3; i++) yield return d.TakeOff(TubeA, Drop());
        for (var i = 0; i < 3; i++) yield return d.TakeOff(TubeB, Drop());
        yield return d.Park();
        yield return Hint("batteries out: the back screws pulse", backScrews);
        for (var n = 1; n <= 4; n++) yield return d.TakeOff(BackScrew(n), default);
        yield return d.Park();
        yield return Hint("back screws out: the back case pulses", (BackCase, Pulse));
        yield return d.TakeOff(BackCase, Drop());
        yield return d.TakeOff(FrontCase, Drop());
        yield return d.Park();

        // Inside, face up: nothing but their own holes around the inner screws, so they show in plain view.
        yield return Toggle();
        yield return d.ExpectIncludes("cases off: the inner screws pulse", (InnerScrew(1), Pulse), (InnerScrew(2), Pulse));
        yield return d.ExpectHidden("the inner screws face the camera: drawn in plain view",
            (InnerScrew(1), false), (InnerScrew(2), false));
        yield return Toggle();
        yield return d.ExpectWithin(0.5f, "R2 again: the hint goes at once");
    }

    // A fresh spot along the lower mat for each part laid down, clear of the parts box and tool cup.
    private Vector2 Drop()
    {
        var i = drops++;
        return Driver.OnScreen(0.27f + 0.065f * (i % 8), i / 8 % 2 == 0 ? 0.2f : 0.29f);
    }

    // One R2 press: Toggle shows the hint, or takes it away. The mod reads R2 only while the bench is idle,
    // not while a part is still going in.
    private IEnumerator Toggle()
    {
        yield return Game.Until(() => WorkbenchSession.Service is { } service && WorkbenchSession.Detecting(service),
            5f, "the bench is idle for R2");
        yield return d.R2(true);
        yield return d.R2(false);
    }

    // R2 pressed, the hint checked, R2 pressed again and the hint checked gone at once.
    private IEnumerator Hint(string label, params (string thing, Mark mark)[] marks)
    {
        yield return Toggle();
        yield return d.Expect(label, marks);
        yield return Toggle();
        yield return d.ExpectWithin(0.5f, label + ", then R2 again: the hint goes at once");
    }

    private static (string, Mark)[] With((string, Mark)[] some, params (string, Mark)[] more)
    {
        var all = new List<(string, Mark)>(some);
        all.AddRange(more);
        return all.ToArray();
    }
}
