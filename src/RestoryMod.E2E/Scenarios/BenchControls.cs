using System.Collections;
using System.Collections.Generic;
using RestoryMod.Features.Fade;
using UnityEngine;
using static RestoryMod.E2E.Mark;

namespace RestoryMod.E2E.Scenarios;

/// <summary>
/// The pad's controls at the workbench, on a perfect Atari Lynx: the controls sign lists the mod's rows
/// and hides the game's zoom-in; R2 with the hint Off does nothing; Y opens and closes the Notepad and X
/// the inventory (swapped from the game); L1 held fades the device so the cursor falls through it; an L2
/// tap zooms all the way in and the next all the way out; a loose part stays outlined for the hover grace
/// after the cursor leaves it; and the right stick spins the device twice as fast at twice the rotation
/// speed.
/// </summary>
internal sealed class BenchControls : IScenario
{
    private const string Model = "Atari_Lynx";
    private const string Cap = "Atari_Lynx_BattaryCap";
    private const string LooseCap = "loose " + Cap;

    public Dictionary<string, string> Options { private get; set; } = new();

    private Driver d = null!;

    public IEnumerator Run(Driver driver)
    {
        d = driver;
        d.Setting("Controller", "R2Hint", Core.HintButtonMode.Hold);
        yield return Game.LoadSave();
        yield return Game.PlaceDevice(Game.Model(Model), new Dictionary<string, Condition>());
        yield return Game.EnterWorkbench();
        yield return d.Park();

        // The sign shows what the pad does here; the game's R2 zoom-in is gone, as L2 toggles the zoom.
        yield return d.ExpectState("the controls sign lists the mod's rows", "the mod's rows",
            "Fade device, Hint, Notepad, Inventory", Screens.AdviceShown);
        yield return d.ExpectState("the controls sign hides the game's zoom-in", "the zoom-in prompt", "hidden",
            Screens.ZoomInPrompt);

        // With the hint Off, R2 is nobody's and the sign says so.
        d.Setting("Controller", "R2Hint", Core.HintButtonMode.Off);
        yield return d.ExpectState("R2 hint Off: the sign drops the hint row", "the mod's rows",
            "Fade device, Notepad, Inventory", Screens.AdviceShown);
        yield return d.R2(true);
        yield return d.ExpectStays("R2 hint Off: R2 held marks nothing", 1f);
        yield return d.R2(false);
        d.Setting("Controller", "R2Hint", Core.HintButtonMode.Hold);

        // Y is the Notepad key (swapped with the inventory's X): it opens the peeking Notepad and puts it back.
        yield return d.ExpectState("the Notepad peeks at the workbench", "the Notepad", "Peeking", Screens.Notepad);
        yield return d.Press(PadButton.Y);
        yield return d.ExpectState("Y: the Notepad opens", "the Notepad", "Open", Screens.Notepad);
        yield return d.Press(PadButton.Y);
        yield return d.ExpectState("Y again: the Notepad peeks again", "the Notepad", "Peeking", Screens.Notepad);

        yield return d.Press(PadButton.X);
        yield return d.ExpectState("X: the inventory opens", "the inventory", "open", Screens.InventoryShown);
        yield return d.Press(PadButton.X);
        yield return d.ExpectState("X again: the inventory closes", "the inventory", "closed", Screens.InventoryShown);

        // L1 held: the device fades and the cursor on the cap now finds nothing; let go, it finds the cap again.
        yield return d.MoveTo(Cap);
        yield return d.ExpectIncludes("the battery cap lights up under the cursor", (Cap, HoverOutline));
        yield return d.Hold(PadButton.LB, true);
        yield return d.ExpectState("L1 held: the device fades", "the device", "faded", Faded);
        yield return d.ExpectIncludes("L1 held: the cursor falls through the faded cap", (Cap, None));
        yield return d.Hold(PadButton.LB, false);
        yield return d.ExpectState("L1 let go: the device is back", "the device", "solid", Faded);
        yield return d.ExpectIncludes("L1 let go: the cap lights up again", (Cap, HoverOutline));
        yield return d.Park();

        // One L2 tap zooms all the way in and stays there; the next, all the way out.
        yield return d.ExpectState("zoomed out at first", "the zoom", "0", Bench.Zoom);
        yield return d.TapL2();
        yield return d.ExpectState("L2 tap: the zoom heads all the way in", "the zoom", "1", Bench.Zoom);
        yield return d.ExpectStateStays("L2 let go: the zoom stays in", "the zoom", "1", Bench.Zoom, 1f);
        yield return d.TapL2();
        yield return d.ExpectState("L2 tap again: the zoom heads all the way out", "the zoom", "0", Bench.Zoom);

        // Hover grace, on a loose part (one on the device gets none): without it the outline goes as soon as
        // the cursor leaves; with it, it lingers, then goes.
        yield return d.TakeOff(Cap, Driver.OnScreen(0.35f, 0.2f));
        yield return d.Park();
        d.Setting("Controller", "HoverGraceMs", 0);
        yield return LeaveCap();
        yield return d.ExpectIncludes("no hover grace: the cap's outline goes once the cursor leaves", (LooseCap, None));
        d.Setting("Controller", "HoverGraceMs", 500);
        yield return LeaveCap();
        yield return d.ExpectIncludes("hover grace: the cap's outline lingers after the cursor leaves", (LooseCap, HoverOutline));
        yield return d.ExpectIncludes("hover grace: then the outline goes", (LooseCap, None));
        yield return d.Park();

        // The device spins by the same amount each frame the stick is held; twice the speed, twice the spin.
        d.Setting("Controller", "RotationSpeed", 0.4f);
        var slow = new SpinMeter();
        yield return Spin(slow);
        d.Setting("Controller", "RotationSpeed", 0.8f);
        var fast = new SpinMeter();
        yield return Spin(fast);
        yield return d.ExpectState("rotation speed 0.8 spins the device twice as fast as 0.4", "spin at 0.8 / spin at 0.4",
            "about 2", () => Twice(fast.DegreesPerFrame / slow.DegreesPerFrame));
    }

    // Within a tenth either way: the first and last frames of a push may turn the device part-way.
    private static string Twice(float ratio) =>
        Mathf.Abs(ratio - 2f) <= 0.2f ? "about 2" : ratio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string Faded() => DeviceFade.IsFaded ? "faded" : "solid";

    // The cursor onto the loose cap, then a short push to the right that takes it off, and a moment's wait:
    // well inside a 500 ms grace, well after the outline would go without one.
    private IEnumerator LeaveCap()
    {
        yield return d.MoveTo(LooseCap);
        yield return d.ExpectIncludes("the loose cap lights up under the cursor", (LooseCap, HoverOutline));
        yield return d.Nudge(new Vec2(1f, 0f), 0.25f);
        yield return Driver.Wait(0.15f);
    }

    // The right stick held half right for a second, each frame's turn of the device recorded.
    private IEnumerator Spin(SpinMeter meter)
    {
        var device = Bench.Device() is { } bench && bench ? bench.transform : throw new StepFailed("no device at the workbench");
        var last = device.rotation;
        d.RightStick(new Vec2(0.5f, 0f));
        var end = Time.unscaledTime + 1f;
        while (Time.unscaledTime < end)
        {
            yield return null;
            meter.Frame(Quaternion.Angle(last, device.rotation));
            last = device.rotation;
        }
        d.RightStick(default);
        yield return Driver.Wait(0.3f);
    }
}
