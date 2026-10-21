using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace RestoryMod.E2E.Scenarios;

/// <summary>
/// The mod's rows in Settings, reached with the pad alone: Start opens the pause menu, Options opens
/// Settings, and the D-pad walks down from "Dialogue volume" through the four rows the mod adds, in order.
/// Left and right move a sensitivity slider a step, and the setting with it. A opens a dropdown in which
/// the D-pad moves the highlight and A picks. The right stick scrolls the list.
/// </summary>
internal sealed class SettingsRows : IScenario
{
    private const string CursorRow = "ControllerSensitivity";
    private const string RotationRow = "RotationSensitivity";
    private const string HintRow = "R2Hint";
    private const string PromptsRow = "ButtonPrompts";

    public Dictionary<string, string> Options { private get; set; } = new();

    private Driver d = null!;

    public IEnumerator Run(Driver driver)
    {
        d = driver;
        // Known values to start from. Claiming each setting the run changes in the menu also keeps the
        // config file unwritten, and puts the old value back at the end.
        d.Setting("Controller", "CursorSpeed", 1.0f);
        d.Setting("Controller", "RotationSpeed", 0.4f);
        d.Setting("Controller", "R2Hint", Core.HintButtonMode.Hold);
        d.Setting("Controller", "ButtonPrompts", Core.PromptStyle.Auto);
        yield return Game.LoadSave();

        yield return d.Press(PadButton.Start);
        yield return d.ExpectState("Start: the pause menu opens", "the pause menu", "shown", Menus.PauseMenu);
        yield return d.Select(Menus.OptionsButton(), PadButton.Down, "Options in the pause menu");
        yield return d.Press(PadButton.A);
        yield return d.ExpectState("A on Options: Settings opens", "Settings", "shown", Menus.Settings);

        yield return d.Select(Menus.DialogueVolume().gameObject, PadButton.Down, "the Dialogue volume slider");
        foreach (var row in new[] { CursorRow, RotationRow, HintRow, PromptsRow })
        {
            yield return d.Press(PadButton.Down);
            yield return d.ExpectState($"D-pad down: {row} comes next", "the selected row", row, Menus.SelectedRow);
        }
        for (var i = 0; i < 3; i++) yield return d.Press(PadButton.Up);
        yield return d.ExpectState("D-pad up three times: back on Controller sensitivity", "the selected row", CursorRow, Menus.SelectedRow);
        yield return d.Press(PadButton.Up);
        yield return d.ExpectState("D-pad up again: back on Dialogue volume", "the selected row", "DialogueVolume", Menus.SelectedRow);
        yield return d.Press(PadButton.Down);

        // The sliders: one step of 0.1 a press, shown to one decimal with no "x".
        yield return d.Press(PadButton.Right);
        yield return d.ExpectState("D-pad right on Controller sensitivity: the cursor speed goes up a step", "the cursor speed",
            "1.1", () => Driver.SettingValue("Controller", "CursorSpeed"));
        yield return d.ExpectState("the slider shows the new cursor speed", "the number beside the slider", "1.1",
            () => Menus.ShownValue(CursorRow));
        yield return d.ExpectState("the cursor speed slider keeps the pad's selection", "the selected row", CursorRow, Menus.SelectedRow);
        yield return d.Press(PadButton.Left);
        yield return d.ExpectState("D-pad left: the cursor speed goes back down", "the cursor speed",
            "1.0", () => Driver.SettingValue("Controller", "CursorSpeed"));

        yield return d.Press(PadButton.Down);
        yield return d.Press(PadButton.Right);
        yield return d.ExpectState("D-pad right on Rotation sensitivity: the rotation speed goes up a step", "the rotation speed",
            "0.5", () => Driver.SettingValue("Controller", "RotationSpeed"));
        yield return d.ExpectState("the slider shows the new rotation speed", "the number beside the slider", "0.5",
            () => Menus.ShownValue(RotationRow));

        // A dropdown opened with A: the D-pad moves the highlight and A picks.
        yield return d.Press(PadButton.Down);
        yield return d.ExpectState("on R2 hint: it shows Hold", "the R2 hint dropdown", "closed, Hold", () => Menus.DropdownState(HintRow));
        yield return d.Press(PadButton.A);
        yield return d.ExpectState("A on R2 hint: its list opens", "the R2 hint dropdown", "open, Hold", () => Menus.DropdownState(HintRow));
        yield return d.ExpectState("the list highlights the option shown", "the highlighted option", "Hold", Menus.HighlightedOption);
        yield return d.Press(PadButton.Down);
        yield return d.ExpectState("D-pad down in the list: the highlight moves to Toggle", "the highlighted option", "Toggle",
            Menus.HighlightedOption);
        yield return d.Press(PadButton.A);
        yield return d.ExpectState("A in the list: Toggle is picked and the list closes", "the R2 hint dropdown", "closed, Toggle",
            () => Menus.DropdownState(HintRow));
        yield return d.ExpectState("the R2 hint setting follows", "the R2 hint setting", "Toggle",
            () => Driver.SettingValue("Controller", "R2Hint"));

        // The right stick scrolls the list the selected row sits in.
        var list = Menus.ScrollListOfSelected() is { } found && found ? found : throw new StepFailed("the R2 hint row sits in no scroll list");
        var before = list.verticalNormalizedPosition;
        d.RightStick(new Vec2(0f, -1f));
        yield return Driver.Wait(0.5f);
        d.RightStick(default);
        yield return d.ExpectState("right stick down: the settings list scrolls down", "the settings list",
            "scrolled down", () => list.verticalNormalizedPosition < before - 0.01f ? "scrolled down"
                : $"at {list.verticalNormalizedPosition.ToString("0.00", CultureInfo.InvariantCulture)}, " +
                  $"from {before.ToString("0.00", CultureInfo.InvariantCulture)}");

        yield return d.Press(PadButton.B);
        yield return d.ExpectState("B: Settings closes", "Settings", "hidden", Menus.Settings);
        yield return d.Press(PadButton.B);
        yield return d.ExpectState("B again: the pause menu closes", "the pause menu", "hidden", Menus.PauseMenu);
    }
}
