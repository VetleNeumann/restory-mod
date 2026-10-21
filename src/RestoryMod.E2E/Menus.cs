using HarmonyLib;
using RestoryMod.Core;
using Restory.UI.Presenters.PauseMenu;
using Restory.UI.Presenters.SettingsMenu;
using Restory.UI.Views.PauseMenu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoryMod.E2E;

/// <summary>Reads the pause menu and Settings as the player sees them, and what the pad has selected there.</summary>
internal static class Menus
{
    /// <summary>The prefix of the rows the mod adds to Settings.</summary>
    private const string ModRow = "RestoryMod_";

    private static T Find<T>() where T : Object =>
        Object.FindAnyObjectByType<T>(FindObjectsInactive.Include) is { } found && found ? found
            : throw new StepFailed($"no {typeof(T).Name} in the scene");

    public static string PauseMenu() => Find<GUI_PauseMenu>().IsShown ? "shown" : "hidden";

    public static string Settings() => Find<GUI_SettingsMenu>().IsVisible ? "shown" : "hidden";

    /// <summary>The pause menu's Options button, which opens Settings.</summary>
    public static GameObject OptionsButton() =>
        Traverse.Create(Find<GUI_PauseMenuView>()).Field<Button>("optionsButton").Value is { } button && button
            ? button.gameObject : throw new StepFailed("the pause menu has no Options button (optionsButton)");

    /// <summary>The game's own Dialogue volume slider, the row the mod's rows go under.</summary>
    public static Slider DialogueVolume()
    {
        var sound = GameTargets.Field(PatchTargets.SettingsMenuSoundSettingsView)!.GetValue(Find<GUI_SettingsMenu>());
        return (Slider)GameTargets.Field(PatchTargets.SoundViewDialogueVolumeSlider)!.GetValue(sound);
    }

    /// <summary>What the pad has selected.</summary>
    public static GameObject? Selected() =>
        EventSystem.current && EventSystem.current.currentSelectedGameObject is { } selected && selected ? selected : null;

    /// <summary>
    /// The settings row the pad has selected: one of the mod's rows by its name ("R2Hint"), "DialogueVolume"
    /// for the game's slider above them, or else the selected object's own name.
    /// </summary>
    public static string SelectedRow()
    {
        var selected = Selected();
        if (!selected) return "nothing";
        if (selected == DialogueVolume().gameObject) return "DialogueVolume";
        for (var t = selected!.transform; t; t = t.parent)
            if (t.name.StartsWith(ModRow)) return t.name.Substring(ModRow.Length);
        return selected.name;
    }

    /// <summary>One of the mod's rows in Settings by name, such as "R2Hint".</summary>
    public static GameObject Row(string name) =>
        GameObject.Find(ModRow + name) is { } row && row ? row : throw new StepFailed($"no '{name}' row in Settings");

    public static Slider SliderOf(string row) =>
        Row(row).GetComponentInChildren<Slider>() is { } slider && slider ? slider : throw new StepFailed($"the '{row}' row has no slider");

    public static TMP_Dropdown DropdownOf(string row) =>
        Row(row).GetComponentInChildren<TMP_Dropdown>() is { } dropdown && dropdown ? dropdown
            : throw new StepFailed($"the '{row}' row has no dropdown");

    /// <summary>The number a slider row shows beside the slider.</summary>
    public static string ShownValue(string row) =>
        SliderOf(row).GetComponentInChildren<TMP_Text>() is { } text && text ? text.text : "no value shown";

    /// <summary>A dropdown row: "open" or "closed", and the option it shows.</summary>
    public static string DropdownState(string row)
    {
        var dropdown = DropdownOf(row);
        var shown = dropdown.value >= 0 && dropdown.value < dropdown.options.Count ? dropdown.options[dropdown.value].text : "none";
        return $"{(dropdown.IsExpanded ? "open" : "closed")}, {shown}";
    }

    /// <summary>The option an open dropdown's list has highlighted, or "none".</summary>
    public static string HighlightedOption()
    {
        var selected = Selected();
        var toggle = selected ? selected!.GetComponent<Toggle>() : null;
        return toggle && toggle!.GetComponentInChildren<TMP_Text>() is { } label && label ? label.text : "none";
    }

    /// <summary>The scroll list around what the pad has selected, or null.</summary>
    public static ScrollRect? ScrollListOfSelected()
    {
        var selected = Selected();
        var list = selected ? selected!.GetComponentInParent<ScrollRect>() : null;
        return list ? list : null;
    }
}
