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

    /// <summary>Pulls L2 fully and lets it go again: one tap.</summary>
    public IEnumerator TapL2()
    {
        pad.Trigger(right: false, 1f);
        yield return Wait(0.15f);
        pad.Trigger(right: false, 0f);
        yield return Wait(0.15f);
    }

    /// <summary>Holds a button down (true) or lets it go, for as long as the scenario needs.</summary>
    public IEnumerator Hold(PadButton button, bool down)
    {
        pad.Button(button, down);
        yield return Wait(0.15f);
    }

    /// <summary>Tilts the right stick, or lets it back to rest with <c>default</c>.</summary>
    public void RightStick(Vec2 tilt) => pad.Stick(right: true, tilt);

    /// <summary>
    /// Tilts the left stick for <paramref name="seconds"/>, then lets it go: the cursor moves off by a
    /// little, by however much the game's cursor speed makes of it, aiming at no thing.
    /// </summary>
    public IEnumerator Nudge(Vec2 tilt, float seconds)
    {
        SeeThrough.Target = null;
        pad.Stick(right: false, tilt);
        yield return Wait(seconds);
        pad.Stick(right: false, default);
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

    /// <summary>Steers the cursor onto the middle of a piece of screen UI, such as a parts box slot.</summary>
    public IEnumerator MoveTo(RectTransform ui, string what)
    {
        var canvas = ui.GetComponentInParent<Canvas>();
        var camera = canvas && canvas!.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        var point = RectTransformUtility.WorldToScreenPoint(camera, ui.TransformPoint(ui.rect.center));
        return MoveTo(point, what);
    }

    /// <summary>
    /// Scrubs the cursor back and forth over a part, a zigzag across all of it on screen, up to
    /// <paramref name="passes"/> times or until <paramref name="done"/> holds. Whatever tool is worked by a
    /// held button must be held by the scenario, which checks the outcome itself.
    /// </summary>
    public IEnumerator Sweep(ElementBase part, System.Func<bool> done, int passes)
    {
        for (var pass = 0; pass < passes && !done(); pass++)
        {
            // Measured again on each pass, as the camera may still be closing in on the part.
            var (min, max) = ScreenBounds(part);
            foreach (var point in SweepPath.Zigzag(min, max, rowSpacing: 20f))
            {
                if (done()) yield break;
                yield return MoveTo(new Vector2(point.X, point.Y), $"a point of the sweep over '{part.name}'");
            }
        }
    }

    // The screen box around the part's meshes, kept on screen.
    private static (Vec2 min, Vec2 max) ScreenBounds(ElementBase part)
    {
        var renderers = part.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
        if (renderers.Count == 0) throw new StepFailed($"'{part.name}' has nothing drawn to sweep over");
        var bounds = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
        var camera = Camera.main;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (var i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var screen = camera.WorldToScreenPoint(corner);
            x0 = Mathf.Min(x0, screen.x);
            y0 = Mathf.Min(y0, screen.y);
            x1 = Mathf.Max(x1, screen.x);
            y1 = Mathf.Max(y1, screen.y);
        }
        return (new Vec2(Mathf.Max(x0, 0f), Mathf.Max(y0, 0f)), new Vec2(Mathf.Min(x1, Screen.width), Mathf.Min(y1, Screen.height)));
    }

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

    /// <summary>
    /// Sends a part away with a D-pad press while holding it (CONTEXT.md, Send): an attached part is taken
    /// off first, a loose one picked up. Done once the part has left the bench and the hand, or for a
    /// destination that keeps it at the bench, once <paramref name="arrived"/> holds for it.
    /// </summary>
    /// <param name="guides">The D-pad directions whose send guides show while the part is held, such as "Down" or "Left,Up".</param>
    public IEnumerator Send(string thing, PadButton direction, string destination, System.Func<ElementBase, bool>? arrived = null,
        string? guides = null)
    {
        var at = Thing(thing);
        ElementBase part;
        yield return MoveTo(thing);
        if (at.Socket)
        {
            var socket = at.Socket!;
            part = socket.NestedElement ? socket.NestedElement : throw new StepFailed($"'{thing}' holds no part");
            yield return HoldA(() => socket.NestedElement != part && part.IsDragging, $"'{thing}' came out in hand", keepHeld: true);
        }
        else
        {
            part = LooseOf(thing);
            yield return HoldA(() => part.IsDragging, $"'{thing}' was picked up", seconds: 3f, keepHeld: true);
        }
        // No guides reads so before any could show, so it must last a moment.
        if (guides == "")
            yield return ExpectStateStays($"holding '{thing}': no send guides", "send guides", guides, SendGuides, 0.5f);
        else if (guides != null)
            yield return ExpectState($"holding '{thing}': send guides show for {guides}", "send guides", guides, SendGuides);
        yield return Press(direction);
        arrived ??= p => !p || !p.IsDragging && Bench.Things().All(t => t.Loose != p);
        yield return Game.Until(() => arrived(part), 5f, $"'{thing}' went to {destination}");
        LetGoOfA();
        yield return Wait(0.3f);
    }

    /// <summary>The directions of the send guides showing now, in sorted order.</summary>
    public static string SendGuides() => string.Join(",", Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
        .Where(t => t.gameObject.activeInHierarchy && t.name.StartsWith("RestoryMod_SendGuide_"))
        .Select(t => t.name.Substring("RestoryMod_SendGuide_".Length))
        .Distinct()
        .OrderBy(d => d, System.StringComparer.Ordinal));

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

    /// <summary>One of the mod's settings as it stands now, numbers to one decimal.</summary>
    public static string SettingValue(string section, string key)
    {
        var value = BepInEx.Bootstrap.Chainloader.PluginInfos["com.zantox.restorymod"].Instance.Config[section, key].BoxedValue;
        return value is float f ? f.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : value.ToString();
    }

    /// <summary>
    /// Presses a D-pad direction until the pad has <paramref name="target"/> selected in a menu; for
    /// getting to the part of a menu under test, not checked itself.
    /// </summary>
    public IEnumerator Select(GameObject target, PadButton direction, string what, int presses = 20)
    {
        for (var i = 0; Menus.Selected() != target; i++)
        {
            if (i == presses)
                throw new StepFailed($"{presses} presses of {direction} never selected {what}; selected: {Menus.SelectedRow()}");
            yield return Press(direction);
        }
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

    // A mark check with no device on the bench would find nothing marked and pass whatever it asked for.
    public static List<BenchThing> ThingsOnBench() =>
        Bench.Device() ? Bench.Things() : throw new StepFailed("no device at the workbench to check the marks of");

    private IEnumerator Check(string label, (string thing, Mark mark)[] marks, bool exact, float seconds)
    {
        if (!ledger.BeginCheck(label)) throw new StopHere();
        var expected = marks.ToDictionary(m => m.thing, m => m.mark);
        var end = Time.unscaledTime + seconds;
        while (true)
        {
            var actual = Bench.Marks(ThingsOnBench());
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
            var actual = Bench.Marks(ThingsOnBench());
            var mismatches = MarkCheck.Compare(expected, actual, exact: true);
            if (mismatches.Count > 0)
            {
                yield return Fail(mismatches, actual);
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// These screws or screw slots are drawn in the hidden style (true) or in plain view (false), waiting
    /// up to the usual time; the others aren't looked at.
    /// </summary>
    public IEnumerator ExpectHidden(string label, params (string thing, bool hidden)[] expected)
    {
        if (!ledger.BeginCheck(label)) throw new StopHere();
        var end = Time.unscaledTime + ExpectSeconds;
        while (true)
        {
            var actual = Bench.Hidden(ThingsOnBench());
            var wrong = expected
                .Where(e => !actual.TryGetValue(e.thing, out var now) || now != e.hidden)
                .Select(e => (e.thing, Style(e.hidden), (string?)(actual.TryGetValue(e.thing, out var now) ? Style(now) : "no screw ghost")))
                .ToList();
            if (wrong.Count == 0) yield break;
            if (Time.unscaledTime > end)
            {
                yield return Fail(wrong);
                yield break;
            }
            yield return null;
        }
    }

    private static string Style(bool hidden) => hidden ? "hidden" : "in view";

    /// <summary>
    /// A check of anything but the marks: <paramref name="what"/> reads <paramref name="expected"/>, as
    /// <paramref name="actual"/> describes it now, within <paramref name="seconds"/>.
    /// </summary>
    public IEnumerator ExpectState(string label, string what, string expected, System.Func<string> actual,
        float seconds = ExpectSeconds)
    {
        if (!ledger.BeginCheck(label)) throw new StopHere();
        var end = Time.unscaledTime + seconds;
        while (true)
        {
            var now = actual();
            if (now == expected) yield break;
            if (Time.unscaledTime > end)
            {
                yield return Fail(new[] { (what, expected, (string?)now) });
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>As <see cref="ExpectState"/>, but it must read so at once and for all of <paramref name="seconds"/>.</summary>
    public IEnumerator ExpectStateStays(string label, string what, string expected, System.Func<string> actual, float seconds)
    {
        if (!ledger.BeginCheck(label)) throw new StopHere();
        var end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end)
        {
            var now = actual();
            if (now != expected)
            {
                yield return Fail(new[] { (what, expected, (string?)now) });
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator Fail(IReadOnlyList<MarkMismatch> mismatches, Dictionary<string, Mark> actual)
    {
        yield return Fail(mismatches.Select(m => (m.Thing, m.Expected.ToString(), m.Actual?.ToString())));
        E2EPlugin.Log("[E2E]   all marks:");
        foreach (var line in Bench.Describe(actual)) E2EPlugin.Log(line);
    }

    private IEnumerator Fail(IEnumerable<(string thing, string expected, string? actual)> wrong)
    {
        var shot = Path.Combine(outDir, $"fail-{ledger.Number}.png");
        ScreenCapture.CaptureScreenshot(shot);
        ledger.FailCheck(wrong, shot);
        foreach (var line in ledger.Report().Skip(Logged)) E2EPlugin.Log(line);
        Logged = ledger.Report().Count;
        // The capture is written at the end of the frame.
        yield return null;
        yield return null;
    }

    /// <summary>How many of the ledger's report lines are in the log already.</summary>
    public int Logged { get; set; }
}
