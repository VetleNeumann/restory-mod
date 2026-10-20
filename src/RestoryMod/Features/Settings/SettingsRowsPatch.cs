using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using RestoryMod.Core;
using RestoryMod.Features.ButtonPrompts;
using RestoryMod.Features.CursorSpeed;
using RestoryMod.Features.DropHint;
using RestoryMod.Features.RotationSpeed;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.Settings;

/// <summary>
/// Adds the mod's rows to the settings menu, below "Dialogue volume": the "Controller sensitivity" and
/// "Rotation sensitivity" sliders, then the "R2 hint" and "Button prompts" dropdowns. The menu is
/// prefab-wired, so each row is cloned on the first Show(): sliders from the dialogue volume row,
/// dropdowns from the monitor row. Unloading the plugin takes the rows out and restores the navigation, so a hot reload
/// adds fresh rows on the next Show().
/// </summary>
[HarmonyPatch]
internal static class SettingsRowsPatch
{
    // A class, not a positional record: the plugin targets netstandard2.1, which lacks IsExternalInit.
    private sealed class SliderRowSpec(string name, string label, Func<ConfigEntry<float>> entry, Func<bool> available)
    {
        public string Name => name;
        public string Label => label;
        public ConfigEntry<float> Entry => entry();
        public bool Available => available();
    }

    private static readonly SliderRowSpec[] SliderRows =
    {
        new("RestoryMod_ControllerSensitivity", "Controller sensitivity", () => CursorSpeedFeature.Speed,
            CursorSpeedFeature.TargetsPresent),
        // No row when the rotation patch skipped itself after a game update: the slider would do nothing.
        new("RestoryMod_RotationSensitivity", "Rotation sensitivity", () => RotationSpeedFeature.Speed,
            RotationSpeedFeature.TargetsPresent),
    };

    private const string HintButtonRowName = "RestoryMod_R2Hint";
    private const string HintButtonLabel = "R2 hint";
    private const string ButtonPromptsRowName = "RestoryMod_ButtonPrompts";
    private const string ButtonPromptsLabel = "Button prompts";

    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.SettingsMenuShow,
        PatchTargets.SettingsMenuSoundSettingsView,
        PatchTargets.SoundViewDialogueVolumeSlider);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.SettingsMenuShow)!;

    private static void Postfix(MonoBehaviour __instance)
    {
        try
        {
            var soundView = GameTargets.Field(PatchTargets.SettingsMenuSoundSettingsView)!.GetValue(__instance);
            if (GameTargets.Field(PatchTargets.SoundViewDialogueVolumeSlider)!.GetValue(soundView) is not Slider template)
            {
                Plugin.Logger.LogWarning("Settings rows: dialogue volume slider not found.");
                return;
            }

            var templateRow = template.transform.parent;
            Selectable above = template;
            var aboveRow = templateRow;
            foreach (var spec in SliderRows)
            {
                if (!spec.Available) continue;

                var row = FindOrCreate(templateRow.parent, spec.Name,
                    () => CreateSliderRow(spec, templateRow, above, aboveRow))!;
                above = row.GetComponentInChildren<Slider>(true);
                aboveRow = row;
            }

            if (!DropdownTargetsPresent()) return;
            if (ScrewHintButtonFeature.TargetsPresent())
            {
                var hintRow = FindOrCreate(templateRow.parent, HintButtonRowName, () => CreateDropdownRow(__instance,
                    HintButtonRowName, HintButtonLabel, above, aboveRow,
                    (dropdown, row) => row.AddComponent<HintButtonRow>().Init(dropdown, ScrewHintButtonFeature.Mode)));
                if (hintRow)
                {
                    above = hintRow!.GetComponentInChildren<TMP_Dropdown>(true);
                    aboveRow = hintRow;
                }
            }
            if (ButtonPromptsFeature.TargetsPresent())
                FindOrCreate(templateRow.parent, ButtonPromptsRowName, () => CreateDropdownRow(__instance,
                    ButtonPromptsRowName, ButtonPromptsLabel, above, aboveRow,
                    (dropdown, row) => row.AddComponent<ButtonPromptsRow>().Init(dropdown, ButtonPromptsFeature.Style)));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Settings rows failed: {e}");
        }
    }

    /// <summary>The row made on an earlier Show(), refreshed, or a new one.</summary>
    private static Transform? FindOrCreate(Transform parent, string name, Func<Transform?> create)
    {
        var row = parent.Find(name);
        if (!row) return create();

        var existing = row.GetComponent<ISettingsRow>();
        if (existing is MonoBehaviour component && component) existing.Refresh();
        return row;
    }

    private static Transform CreateSliderRow(SliderRowSpec spec, Transform templateRow, Selectable above, Transform aboveRow) =>
        CloneRow(templateRow, spec.Name, spec.Label, above, aboveRow, row =>
        {
            var slider = row.GetComponentInChildren<Slider>(true);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.wholeNumbers = true;
            slider.minValue = 0;
            slider.maxValue = Sensitivity.SliderSteps;

            // Slider only uses left/right to change its value while those links are empty, so keep the template's.
            SpliceNavigation(above, slider, clearSideLinks: false);
            row.AddComponent<SensitivityRow>().Init(slider, slider.GetComponentInChildren<TMP_Text>(true), spec.Entry);
        });

    private static bool DropdownTargetsPresent() => GameTargets.AllPresent(
        PatchTargets.SettingsMenuMainView,
        PatchTargets.MainViewMonitorDropdown,
        PatchTargets.RestoryDropdownContainer);

    /// <summary>A dropdown row cloned from the monitor row; <paramref name="bind"/> wires it to its setting.</summary>
    private static Transform? CreateDropdownRow(MonoBehaviour menu, string name, string label, Selectable above,
        Transform aboveRow, Action<TMP_Dropdown, GameObject> bind)
    {
        var mainView = GameTargets.Field(PatchTargets.SettingsMenuMainView)!.GetValue(menu);
        if (GameTargets.Field(PatchTargets.MainViewMonitorDropdown)!.GetValue(mainView) is not TMP_Dropdown template || !template)
        {
            Plugin.Logger.LogWarning("Settings rows: monitor dropdown not found.");
            return null;
        }
        // The game's dropdown builds its list through Zenject; the clone needs the same container.
        if (GameTargets.Field(PatchTargets.RestoryDropdownContainer)!.GetValue(template) is not DiContainer container)
        {
            Plugin.Logger.LogWarning("Settings rows: monitor dropdown has no Zenject container yet.");
            return null;
        }

        return CloneRow(template.transform.parent, name, label, above, aboveRow, row =>
        {
            var dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            // A clone's components are new and uninjected; inject them as the game's prefab instantiation would.
            container.InjectGameObject(row);

            SpliceNavigation(above, dropdown, clearSideLinks: true);
            bind(dropdown, row);
        });
    }

    /// <summary>
    /// Clones a settings row, relabels it and inserts it below <paramref name="aboveRow"/> (which may sit in
    /// another section than the template). <paramref name="bind"/> wires the clone's control.
    /// </summary>
    private static Transform CloneRow(Transform templateRow, string name, string label, Selectable above,
        Transform aboveRow, Action<GameObject> bind)
    {
        // Clone under an inactive holder so no Awake/OnEnable runs before we strip game components.
        // The holder is always destroyed; if anything throws before the row is moved out, the
        // half-built row goes with it.
        var holder = new GameObject("RestoryMod_CloneHolder");
        holder.SetActive(false);
        try
        {
            var row = Object.Instantiate(templateRow.gameObject, holder.transform);
            row.name = name;
            Plugin.Teardown.Add(() =>
            {
                if (!row) return;
                // Destroy waits for the end of the frame; renamed, the row can't be mistaken for ours by then.
                row.name += "_Removed";
                Object.Destroy(row);
            });

            foreach (var component in row.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Localisation would overwrite our label; the value formatter would overwrite our text.
                var typeName = component.GetType().Name;
                if (typeName is "GUI_LocalisedText" or "GUI_SliderTextValue")
                    Object.DestroyImmediate(component);
            }

            var labelText = row.transform.GetChild(0).GetComponent<TMP_Text>();
            if (labelText) labelText.text = label;

            var navigationMode = above.navigation.mode;
            bind(row);
            row.transform.SetParent(aboveRow.parent, false);
            row.transform.SetSiblingIndex(aboveRow.GetSiblingIndex() + 1);

            Plugin.Logger.LogInfo($"{label} row added (navigation: {navigationMode}).");
            return row.transform;
        }
        finally
        {
            Object.Destroy(holder);
        }
    }

    /// <summary>
    /// With explicit navigation, splice the control in between the row above and whatever was below
    /// it. Up/down are rewired; left/right are either kept from the template or cleared, since the
    /// template's point at its own neighbours. Automatic navigation needs nothing. Registers the step that
    /// puts the links back on unload as soon as they change, so they never point at a lost control.
    /// </summary>
    private static void SpliceNavigation(Selectable above, Selectable inserted, bool clearSideLinks)
    {
        var aboveNav = above.navigation;
        if (aboveNav.mode != Navigation.Mode.Explicit) return;
        var aboveBefore = aboveNav;

        var below = aboveNav.selectOnDown;

        var insertedNav = inserted.navigation;
        insertedNav.mode = Navigation.Mode.Explicit;
        insertedNav.selectOnUp = above;
        insertedNav.selectOnDown = below;
        if (clearSideLinks)
        {
            insertedNav.selectOnLeft = null;
            insertedNav.selectOnRight = null;
        }
        inserted.navigation = insertedNav;

        aboveNav.selectOnDown = inserted;
        above.navigation = aboveNav;

        var belowRewired = below && below.navigation.mode == Navigation.Mode.Explicit && below.navigation.selectOnUp == above;
        var belowBefore = belowRewired ? below!.navigation : default;
        if (belowRewired)
        {
            var belowNav = below!.navigation;
            belowNav.selectOnUp = inserted;
            below.navigation = belowNav;
        }

        Plugin.Teardown.Add(() =>
        {
            if (above) above.navigation = aboveBefore;
            if (belowRewired && below) below.navigation = belowBefore;
        });
    }
}

/// <summary>A mod row in the settings menu.</summary>
internal interface ISettingsRow
{
    /// <summary>Pulls the current config value into the control (it may have been edited by hand).</summary>
    void Refresh();
}

/// <summary>Binds a cloned slider to a sensitivity config entry.</summary>
internal sealed class SensitivityRow : MonoBehaviour, ISettingsRow
{
    private Slider slider = null!;
    private TMP_Text? valueText;
    private ConfigEntry<float> entry = null!;

    public void Init(Slider slider, TMP_Text? valueText, ConfigEntry<float> entry)
    {
        this.slider = slider;
        this.valueText = valueText;
        this.entry = entry;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnEnable()
    {
        if (slider) Refresh();
    }

    private float Current => Sensitivity.Normalize(entry.Value);

    public void Refresh()
    {
        slider.SetValueWithoutNotify(Sensitivity.ToStep(Current));
        UpdateText();
    }

    private void OnSliderChanged(float step)
    {
        entry.Value = Sensitivity.FromStep((int)step);
        UpdateText();
    }

    private void UpdateText()
    {
        if (valueText) valueText!.text = Sensitivity.Format(Current);
    }
}

/// <summary>
/// Binds a cloned dropdown to the "Button prompts" setting. The Auto entry names the pad in hand, e.g.
/// "Auto (PlayStation)", and follows it when another pad is detected.
/// </summary>
internal sealed class ButtonPromptsRow : MonoBehaviour, ISettingsRow
{
    private TMP_Dropdown dropdown = null!;
    private ConfigEntry<PromptStyle> entry = null!;
    private bool refreshWhenClosed;

    public void Init(TMP_Dropdown dropdown, ConfigEntry<PromptStyle> entry)
    {
        this.dropdown = dropdown;
        this.entry = entry;
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    private void OnEnable()
    {
        ButtonPromptsPoller.DetectedChanged += Refresh;
        if (dropdown) Refresh();
    }

    private void OnDisable() => ButtonPromptsPoller.DetectedChanged -= Refresh;

    public void Refresh()
    {
        if (!dropdown) return;
        // Rebuilding the options under an open list would leave its rows stale: wait for it to close.
        if (dropdown.IsExpanded)
        {
            refreshWhenClosed = true;
            return;
        }
        refreshWhenClosed = false;

        var detected = ButtonPromptsPoller.Detected;
        dropdown.ClearOptions();
        dropdown.AddOptions(PromptStyles.InMenuOrder.Select(style => PromptStyles.Label(style, detected)).ToList());
        dropdown.SetValueWithoutNotify(PromptStyles.ToIndex(entry.Value));
        dropdown.RefreshShownValue();
    }

    private void Update()
    {
        if (refreshWhenClosed && dropdown && !dropdown.IsExpanded) Refresh();
    }

    private void OnDropdownChanged(int index) => entry.Value = PromptStyles.FromIndex(index);
}

/// <summary>Binds a cloned dropdown to the "R2 hint" setting: Hold, Toggle or Off.</summary>
internal sealed class HintButtonRow : MonoBehaviour, ISettingsRow
{
    private static readonly HintButtonMode[] InMenuOrder = { HintButtonMode.Hold, HintButtonMode.Toggle, HintButtonMode.Off };

    private TMP_Dropdown dropdown = null!;
    private ConfigEntry<HintButtonMode> entry = null!;

    public void Init(TMP_Dropdown dropdown, ConfigEntry<HintButtonMode> entry)
    {
        this.dropdown = dropdown;
        this.entry = entry;
        dropdown.ClearOptions();
        dropdown.AddOptions(InMenuOrder.Select(mode => mode.ToString()).ToList());
        dropdown.onValueChanged.AddListener(index => this.entry.Value = InMenuOrder[index]);
    }

    private void OnEnable()
    {
        if (dropdown) Refresh();
    }

    public void Refresh()
    {
        if (!dropdown || dropdown.IsExpanded) return;
        dropdown.SetValueWithoutNotify(Math.Max(0, Array.IndexOf(InMenuOrder, entry.Value)));
        dropdown.RefreshShownValue();
    }
}
