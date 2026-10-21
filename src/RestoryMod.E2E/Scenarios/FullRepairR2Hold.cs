using System.Collections;
using System.Collections.Generic;
using static RestoryMod.E2E.Mark;

namespace RestoryMod.E2E.Scenarios;

/// <summary>
/// A complete repair of an Atari CX40 joystick with the R2 hint on Hold: its button damaged, its board
/// dirty, every part unknown at first. After each step R2 shows the hint and the check names every
/// mark that must be on the bench (and so none elsewhere); letting go of R2 must clear them.
/// </summary>
internal sealed class FullRepairR2Hold : IScenario
{
    private const string Model = "Atari_CX40";
    private const string P = "Atari_CX40_Joystick_";
    private const string Lid = P + "Lid";
    private const string Button = P + "Button";
    private const string Board = P + "Board";
    private const string Wire = P + "Wire";
    private const string Base = P + "Base";
    private const string BotPart = P + "Bot_Part";

    private static string Screw(int n) => $"{P}Screw #{n}";

    public Dictionary<string, string> Options { private get; set; } = new();

    private Driver d = null!;

    public IEnumerator Run(Driver driver)
    {
        d = driver;
        d.Setting("Controller", "R2Hint", Core.HintButtonMode.Hold);
        d.Setting("Workbench", "HintShowsEveryPart", true);
        yield return Game.LoadSave();
        yield return Game.PlaceDevice(Game.Model(Model), new Dictionary<string, Condition>
        {
            [Button] = Condition.Damaged,
            [Board] = Condition.Dirty,
        });
        yield return Game.EnterWorkbench();
        yield return d.Park();

        yield return d.ExpectStays("nothing marked before R2", 1f);
        yield return d.R2(true);
        yield return d.Expect("R2 held: the four screws pulse",
            (Screw(1), Pulse), (Screw(2), Pulse), (Screw(3), Pulse), (Screw(4), Pulse));
        yield return d.R2(false);
        yield return d.ExpectWithin(5f, "R2 let go: the pulse ends");

        for (var n = 1; n <= 3; n++)
        {
            yield return d.TakeOff(Screw(n), default);
            yield return d.Park();
            var left = new List<(string, Mark)>();
            for (var m = n + 1; m <= 4; m++) left.Add((Screw(m), Pulse));
            yield return Hint($"screw {n} out: the other screws still pulse", left.ToArray());
        }
        yield return d.TakeOff(Screw(4), default);
        yield return d.Park();
        yield return Hint("screws out: the lid pulses", (Lid, Pulse));

        yield return d.TakeOff(Lid, Driver.OnScreen(0.25f, 0.3f));
        yield return d.Park();
        // Every part on top can come out now, so R2 shows each one's condition: the button is damaged.
        // The board, under them, stays unknown.
        yield return Hint("lid off: everything on top pulses, the button in the damaged colour",
            (Button, DamagedPulse), (Wire, Pulse), (Base, Pulse));

        yield return d.TakeOff(Button, Driver.OnScreen(0.35f, 0.2f));
        yield return d.Park();
        // The damaged button, out on the bench, still needs throwing away before the device is done.
        yield return Hint("button off: the rest on top still pulses, and the damaged button on the bench",
            (Wire, Pulse), (Base, Pulse), (Loose(Button), DamagedPulse));

        yield return d.TakeOff(Wire, Driver.OnScreen(0.45f, 0.2f));
        yield return d.Park();
        yield return Hint("wire off: the base still pulses", (Base, Pulse), (Loose(Button), DamagedPulse));
        yield return d.TakeOff(Base, Driver.OnScreen(0.55f, 0.2f));
        yield return d.Park();
        yield return Hint("wire and base off: the board pulses in the dirty colour",
            (Board, DirtyPulse), (Loose(Button), DamagedPulse));

        yield return d.TakeOff(Board, Driver.OnScreen(0.65f, 0.2f));
        yield return d.Park();
        // The bottom part, shown fine and with nothing behind it, isn't asked for; the board and the
        // button still need work, so the hint stays on taking apart instead of offering them back.
        yield return Hint("board off: the dirty board and the damaged button pulse, nothing goes back yet",
            (Loose(Board), DirtyPulse), (Loose(Button), DamagedPulse));
        yield return d.TakeOff(BotPart, Driver.OnScreen(0.75f, 0.25f));
        yield return d.Park();

        // Not under test: the board is cleaned and the damaged button thrown away by the game itself.
        Game.Clean(d.Thing(Loose(Board)).Loose!);
        Game.Discard(d.Thing(Loose(Button)).Loose!);
        yield return Driver.Wait(0.5f);

        // Nothing left that needs work or is unknown: the hint shows putting back.
        // The ready loose parts pulse where they lie; only what goes in now.
        yield return Hint("all apart: the bottom part pulses", (Loose(BotPart), Pulse));
        yield return d.PutBack(Loose(BotPart), BotPart);
        yield return d.Park();
        yield return Hint("bottom part in: the board and the wire pulse", (Loose(Board), Pulse), (Loose(Wire), Pulse));
        yield return d.PutBack(Loose(Board), Board);
        yield return d.Park();
        yield return Hint("board in: base and wire pulse, the button's socket is missing, in red",
            (Loose(Base), Pulse), (Loose(Wire), Pulse), (Button, MissingGhost));

        // A spare from the parts box.
        yield return Game.SpawnSpare(Game.Element(Model, Button));
        yield return Hint("spare button on the bench: it pulses and the red ghost goes",
            (Loose(Base), Pulse), (Loose(Wire), Pulse), (Loose(Button), Pulse));
        yield return d.PutBack(Loose(Button), Button);
        yield return d.PutBack(Loose(Wire), Wire);
        yield return d.PutBack(Loose(Base), Base);
        yield return d.Park();
        yield return Hint("inside back together: the lid pulses", (Loose(Lid), Pulse));
        yield return d.PutBack(Loose(Lid), Lid);
        yield return d.Park();
        yield return Hint("lid on: the four screw slots show their ghosts",
            (Screw(1), Ghost), (Screw(2), Ghost), (Screw(3), Ghost), (Screw(4), Ghost));

        for (var n = 1; n <= 4; n++) yield return d.ScrewIn(Screw(n));
        yield return d.Park();
        yield return Game.Until(Game.Restored, 15f, "the device passed the game's check as restored");
        yield return d.R2(true);
        yield return d.ExpectStays("restored: R2 marks nothing", 1f);
        yield return d.R2(false);
    }

    private static string Loose(string part) => "loose " + part;

    // R2 pulled, the hint checked, R2 let go and the pulse checked gone.
    private IEnumerator Hint(string label, params (string thing, Mark mark)[] marks)
    {
        yield return d.R2(true);
        yield return d.Expect(label, marks);
        yield return d.R2(false);
        yield return d.ExpectWithin(5f, label + ", then R2 let go: the pulse ends");
    }
}
