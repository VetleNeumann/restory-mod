using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Restory.Gameplay.Elements;
using Restory.Gameplay.GameCursor;
using Rewired.Components;
using UnityEngine;

namespace RestoryMod.E2E;

/// <summary>Thrown at the --stop-at check: the run halts there on purpose.</summary>
internal sealed class StopHere : System.Exception { }

/// <summary>
/// The steps a scenario is written in: pad input under test (moving the cursor, pressing, holding) and
/// checks of the marks at the workbench. A check waits for the marks to match, as Playwright's expect
/// does; a failed check is recorded and the run goes on, a step that can't be carried out ends the run.
/// </summary>
internal sealed class Driver
{
    /// <summary>How long a check waits for the marks to match before it fails.</summary>
    private const float ExpectSeconds = 2f;

    private readonly VirtualPad pad;
    private readonly RunLedger ledger;
    private readonly string outDir;
    private readonly StickSteer steer = new(tolerance: 6f, slowWithin: 150f, minTilt: 0.3f);

    public Driver(VirtualPad pad, RunLedger ledger, string outDir)
    {
        this.pad = pad;
        this.ledger = ledger;
        this.outDir = outDir;
    }

    public VirtualPad Pad => pad;

    /// <summary>The thing at the workbench by name, or a failed step listing what is there.</summary>
    public BenchThing Thing(string name)
    {
        var things = Bench.Things();
        return things.FirstOrDefault(t => t.Name == name)
            ?? throw new StepFailed($"no '{name}' at the workbench; there: {string.Join(", ", things.Select(t => t.Name))}");
    }

    // Unity's fake null: a destroyed socket or part must fail here, so no `??`.
    private ElementSocket SocketOf(string thing)
    {
        var socket = Thing(thing).Socket;
        return socket ? socket! : throw new StepFailed($"'{thing}' is not a socket");
    }

    private ElementBase LooseOf(string thing)
    {
        var part = Thing(thing).Loose;
        return part ? part! : throw new StepFailed($"'{thing}' is not a loose part");
    }

    public static IEnumerator Wait(float seconds)
    {
        var end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
    }

    public IEnumerator Press(PadButton button, float hold = 0.1f)
    {
        pad.Button(button, true);
        yield return Wait(hold);
        pad.Button(button, false);
        yield return Wait(0.1f);
    }

    /// <summary>Pulls R2 fully (true) or lets it go.</summary>
    public IEnumerator R2(bool pulled)
    {
        pad.Trigger(right: true, pulled ? 1f : 0f);
        yield return Wait(0.15f);
    }

    private static Vector2 Cursor() =>
        Object.FindAnyObjectByType<PlayerMouse>() is { } mouse && mouse ? mouse.screenPosition
            : throw new StepFailed("no virtual cursor");

    /// <summary>Steers the virtual cursor with the left stick onto a screen point, aiming at no thing.</summary>
    public IEnumerator MoveTo(Vector2 target, string what)
    {
        SeeThrough.Target = null;
        return Steer(target, what);
    }

    private IEnumerator Steer(Vector2 target, string what)
    {
        var end = Time.unscaledTime + 8f;
        var goal = new Vec2(target.x, target.y);
        while (true)
        {
            var at = Cursor();
            var now = new Vec2(at.x, at.y);
            if (steer.Arrived(now, goal)) break;
            if (Time.unscaledTime > end)
            {
                pad.Stick(right: false, default);
                throw new StepFailed($"the cursor never reached {what} (at {at}, aiming at {target})");
            }
            pad.Stick(right: false, steer.Tilt(now, goal));
            yield return null;
        }
        pad.Stick(right: false, default);
        yield return Wait(0.15f);
    }

    /// <summary>
    /// Steers the cursor onto a thing at the workbench until the game detects it under the cursor. The
    /// cursor sees through whatever lies in front of it (<see cref="SeeThrough"/>) until the next move.
    /// </summary>
    public IEnumerator MoveTo(string thing)
    {
        var target = Thing(thing);
        var point = target.ScreenPoint(Camera.main) ?? throw new StepFailed($"'{thing}' is off screen");
        SeeThrough.Target = target.Part ? target.Part!.transform : target.Socket!.transform;
        yield return Steer(point, $"'{thing}'");
        var selection = Game.Resolve<CursorSelectionService>();
        var end = Time.unscaledTime + 2f;
        while (!Detected(target, selection.DetectedGameObject))
        {
            if (Time.unscaledTime > end)
            {
                var detected = selection.DetectedGameObject;
                var colliders = SeeThrough.Target!.GetComponentsInChildren<Collider>(includeInactive: true)
                    .Select(c => $"{c.name} (layer {c.gameObject.layer}, {(c.enabled && c.gameObject.activeInHierarchy ? "on" : "off")})");
                throw new StepFailed($"the game never detected '{thing}' under the cursor; it detected " +
                    $"{(detected ? $"'{detected!.name}' (layer {detected.layer})" : "nothing")}; '{thing}' has colliders: " +
                    string.Join(", ", colliders));
            }
            yield return null;
        }
    }

    private static bool Detected(BenchThing thing, GameObject? detected)
    {
        if (!detected) return false;
        var own = thing.Part ? thing.Part!.transform : thing.Socket ? thing.Socket!.transform : null;
        return own && detected!.transform.IsChildOf(own);
    }

    /// <summary>Holds A until <paramref name="done"/> holds; A stays down while <paramref name="keepHeld"/>.</summary>
    public IEnumerator HoldA(System.Func<bool> done, string what, float seconds = 10f, bool keepHeld = false)
    {
        pad.Button(PadButton.A, true);
        yield return Game.Until(done, seconds, what);
        if (keepHeld) yield break;
        pad.Button(PadButton.A, false);
        yield return Wait(0.2f);
    }

    public void LetGoOfA() => pad.Button(PadButton.A, false);

    /// <summary>Moves the cursor off the device, to the top left of the screen, so it hovers nothing.</summary>
    public IEnumerator Park() => MoveTo(new Vector2(Screen.width * 0.1f, Screen.height * 0.85f), "the parking spot");

    /// <summary>
    /// Takes a part off the device by hand: hover it, hold A until it comes out, and for a part (not a
    /// screw, which drops in the bin by itself) carry it to <paramref name="dropAt"/> and let go.
    /// </summary>
    public IEnumerator TakeOff(string thing, Vector2 dropAt)
    {
        var socket = SocketOf(thing);
        var part = socket.NestedElement ? socket.NestedElement : throw new StepFailed($"'{thing}' holds no part");
        var screw = part.Info.Category == Restory.Data.Elements.ElementCategory.Small;
        yield return MoveTo(thing);
        yield return HoldA(() => socket.NestedElement != part, $"'{thing}' came out", keepHeld: !screw);
        if (screw) yield break;
        yield return MoveTo(dropAt, $"the drop spot for '{thing}'");
        LetGoOfA();
        yield return Game.Until(() => part && part.IsOnSurface && !part.IsDragging, 5f, $"'{thing}' lay on the bench");
        yield return Wait(0.3f);
    }

    /// <summary>Carries a loose part onto its socket and lets go there.</summary>
    public IEnumerator PutBack(string loose, string socketThing)
    {
        var part = LooseOf(loose);
        var socket = SocketOf(socketThing);
        yield return MoveTo(loose);
        yield return HoldA(() => part.IsDragging, $"'{loose}' was picked up", seconds: 3f, keepHeld: true);
        var target = Thing(socketThing).ScreenPoint(Camera.main) ?? throw new StepFailed($"'{socketThing}' is off screen");
        yield return MoveTo(target, $"'{socketThing}'");
        LetGoOfA();
        yield return Game.Until(() => socket.NestedElement == part && !part.IsInstalling, 5f, $"'{loose}' went into '{socketThing}'");
        yield return Wait(0.3f);
    }

    /// <summary>Puts a screw back in its slot: press the slot, then hold A until it is screwed in.</summary>
    public IEnumerator ScrewIn(string socketThing)
    {
        var socket = SocketOf(socketThing);
        yield return MoveTo(socketThing);
        yield return HoldA(() => socket.NestedElement && !socket.NestedElement.IsInstalling && socket.NestedElement.Progress <= 0f,
            $"a screw was screwed into '{socketThing}'");
    }

    private readonly List<System.Action> restore = new();

    /// <summary>
    /// Sets one of the mod's settings for this run only: the config file is never written, and
    /// <see cref="RestoreSettings"/> puts the old value back.
    /// </summary>
    public void Setting(string section, string key, object value)
    {
        var config = BepInEx.Bootstrap.Chainloader.PluginInfos["com.zantox.restorymod"].Instance.Config;
        if (!config.ContainsKey(new BepInEx.Configuration.ConfigDefinition(section, key)))
            throw new StepFailed($"the mod has no setting [{section}] {key}");
        var entry = config[section, key];
        var old = entry.BoxedValue;
        var saving = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        entry.BoxedValue = value;
        restore.Add(() =>
        {
            entry.BoxedValue = old;
            config.SaveOnConfigSet = saving;
        });
    }

    public void RestoreSettings()
    {
        for (var i = restore.Count - 1; i >= 0; i--) restore[i]();
        restore.Clear();
    }

    /// <summary>A point on screen relative to the screen size (0..1 each way, y up).</summary>
    public static Vector2 OnScreen(float x, float y) => new(Screen.width * x, Screen.height * y);

    /// <summary>Exactly these things carry these marks, every other thing none.</summary>
    public IEnumerator Expect(string label, params (string thing, Mark mark)[] marks) =>
        Check(label, marks, exact: true, ExpectSeconds);

    /// <summary>As <see cref="Expect"/>, waiting up to <paramref name="seconds"/> for the marks to match.</summary>
    public IEnumerator ExpectWithin(float seconds, string label, params (string thing, Mark mark)[] marks) =>
        Check(label, marks, exact: true, seconds);

    /// <summary>These things carry these marks; the others aren't looked at.</summary>
    public IEnumerator ExpectIncludes(string label, params (string thing, Mark mark)[] marks) =>
        Check(label, marks, exact: false, ExpectSeconds);

    private IEnumerator Check(string label, (string thing, Mark mark)[] marks, bool exact, float seconds)
    {
        if (!ledger.BeginCheck(label)) throw new StopHere();
        var expected = marks.ToDictionary(m => m.thing, m => m.mark);
        var end = Time.unscaledTime + seconds;
        while (true)
        {
            var actual = Bench.Marks(Bench.Things());
            var mismatches = MarkCheck.Compare(expected, actual, exact);
            if (mismatches.Count == 0) yield break;
            if (Time.unscaledTime > end)
            {
                yield return Fail(mismatches, actual);
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>The marks stay exactly so for <paramref name="seconds"/>: nothing appears or goes.</summary>
    public IEnumerator ExpectStays(string label, float seconds, params (string thing, Mark mark)[] marks)
    {
        if (!ledger.BeginCheck(label)) throw new StopHere();
        var expected = marks.ToDictionary(m => m.thing, m => m.mark);
        var end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end)
        {
            var actual = Bench.Marks(Bench.Things());
            var mismatches = MarkCheck.Compare(expected, actual, exact: true);
            if (mismatches.Count > 0)
            {
                yield return Fail(mismatches, actual);
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator Fail(IReadOnlyList<MarkMismatch> mismatches, Dictionary<string, Mark> actual)
    {
        var shot = Path.Combine(outDir, $"fail-{ledger.Number}.png");
        ScreenCapture.CaptureScreenshot(shot);
        ledger.FailCheck(mismatches, shot);
        foreach (var line in ledger.Report().Skip(Logged)) E2EPlugin.Log(line);
        Logged = ledger.Report().Count;
        E2EPlugin.Log("[E2E]   all marks:");
        foreach (var line in Bench.Describe(actual)) E2EPlugin.Log(line);
        // The capture is written at the end of the frame.
        yield return null;
        yield return null;
    }

    /// <summary>How many of the ledger's report lines are in the log already.</summary>
    public int Logged { get; set; }
}
