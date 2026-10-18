using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using RestoryMod.Core;
using RestoryMod.Features.ButtonPrompts;
using RestoryMod.Features.CursorSpeed;
using RestoryMod.Features.RotationSpeed;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.Settings;

/// <summary>
/// Adds the mod's rows to the settings menu: "Controller sensitivity", "Rotation sensitivity" and
/// "Button prompts". The menu is prefab-wired, so each row is cloned from the dialogue volume row and
/// inserted below it (and below each other) on the first Show(). Every row is a stepped slider, so
/// left/right on the D-pad changes it.
/// </summary>
[HarmonyPatch]
internal static class SettingsRowsPatch
{
    // A class, not a positional record: the plugin targets netstandard2.1, which lacks IsExternalInit.
    private sealed class RowSpec(string name, string label, int steps, Func<bool> available,
        Func<GameObject, Slider, TMP_Text?, ISettingsRow> attach)
    {
        public string Name => name;
        public string Label => label;
        public int Steps => steps;
        public bool Available => available();
        public ISettingsRow Attach(GameObject row, Slider slider, TMP_Text? valueText) => attach(row, slider, valueText);
    }

    private static readonly RowSpec[] Rows =
    {
        new("RestoryMod_ControllerSensitivity", "Controller sensitivity", Sensitivity.SliderSteps,
            () => GameTargets.AllPresent(PatchTargets.CursorControlsTypeChanged, PatchTargets.CursorPlayerMouse),
            (row, slider, text) => row.AddComponent<SensitivityRow>().Init(slider, text, CursorSpeedFeature.Speed)),
        // No row when the rotation patch skipped itself after a game update: the slider would do nothing.
        new("RestoryMod_RotationSensitivity", "Rotation sensitivity", Sensitivity.SliderSteps,
            RotationSpeedFeature.TargetsPresent,
            (row, slider, text) => row.AddComponent<SensitivityRow>().Init(slider, text, RotationSpeedFeature.Speed)),
        new("RestoryMod_ButtonPrompts", "Button prompts", PromptStyles.InSliderOrder.Length - 1,
            ButtonPromptsFeature.TargetsPresent,
            (row, slider, text) => row.AddComponent<ButtonPromptsRow>().Init(slider, text, ButtonPromptsFeature.Style)),
    };

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
            foreach (var spec in Rows)
            {
                if (!spec.Available) continue;

                var row = templateRow.parent.Find(spec.Name);
                if (row)
                {
                    var existing = row.GetComponent<ISettingsRow>();
                    if (existing is MonoBehaviour component && component) existing.Refresh();
                }
                else
                    row = CreateRow(spec, templateRow, above, aboveRow);

                above = row.GetComponentInChildren<Slider>(true);
                aboveRow = row;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Settings rows failed: {e}");
        }
    }

    private static Transform CreateRow(RowSpec spec, Transform templateRow, Selectable above, Transform aboveRow)
    {
        // Clone under an inactive holder so no Awake/OnEnable runs before we strip game components.
        // The holder is always destroyed; if anything throws before the row is moved out, the
        // half-built row goes with it.
        var holder = new GameObject("RestoryMod_CloneHolder");
        holder.SetActive(false);
        try
        {
            var row = Object.Instantiate(templateRow.gameObject, holder.transform);
            row.name = spec.Name;

            foreach (var component in row.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Localisation would overwrite our label; the value formatter would overwrite our text.
                var typeName = component.GetType().Name;
                if (typeName is "GUI_LocalisedText" or "GUI_SliderTextValue")
                    Object.DestroyImmediate(component);
            }

            var slider = row.GetComponentInChildren<Slider>(true);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.wholeNumbers = true;
            slider.minValue = 0;
            slider.maxValue = spec.Steps;

            var label = row.transform.GetChild(0).GetComponent<TMP_Text>();
            if (label != null) label.text = spec.Label;
            var valueText = slider.GetComponentInChildren<TMP_Text>(true);

            var navigationMode = above.navigation.mode;
            SpliceNavigation(above, slider);

            spec.Attach(row, slider, valueText);
            row.transform.SetParent(templateRow.parent, false);
            row.transform.SetSiblingIndex(aboveRow.GetSiblingIndex() + 1);

            Plugin.Logger.LogInfo($"{spec.Label} row added (navigation: {navigationMode}).");
            return row.transform;
        }
        finally
        {
            Object.Destroy(holder);
        }
    }

    /// <summary>
    /// With explicit navigation, splice the slider in between the row above and whatever was below
    /// it. Only up/down are rewired: the slider keeps the template slider's own left/right links,
    /// because Slider only uses left/right to change its value when those links are empty.
    /// Automatic navigation needs nothing.
    /// </summary>
    private static void SpliceNavigation(Selectable above, Slider inserted)
    {
        var aboveNav = above.navigation;
        if (aboveNav.mode != Navigation.Mode.Explicit) return;

        var below = aboveNav.selectOnDown;

        var insertedNav = inserted.navigation;
        insertedNav.mode = Navigation.Mode.Explicit;
        insertedNav.selectOnUp = above;
        insertedNav.selectOnDown = below;
        inserted.navigation = insertedNav;

        aboveNav.selectOnDown = inserted;
        above.navigation = aboveNav;

        if (below != null && below.navigation.mode == Navigation.Mode.Explicit && below.navigation.selectOnUp == above)
        {
            var belowNav = below.navigation;
            belowNav.selectOnUp = inserted;
            below.navigation = belowNav;
        }
    }
}

/// <summary>A mod row in the settings menu.</summary>
internal interface ISettingsRow
{
    /// <summary>Pulls the current config value into the slider (it may have been edited by hand).</summary>
    void Refresh();
}

/// <summary>Binds a cloned slider to a sensitivity config entry.</summary>
internal sealed class SensitivityRow : MonoBehaviour, ISettingsRow
{
    private Slider slider = null!;
    private TMP_Text? valueText;
    private ConfigEntry<float> entry = null!;

    public SensitivityRow Init(Slider slider, TMP_Text? valueText, ConfigEntry<float> entry)
    {
        this.slider = slider;
        this.valueText = valueText;
        this.entry = entry;
        slider.onValueChanged.AddListener(OnSliderChanged);
        return this;
    }

    private void OnEnable()
    {
        if (slider != null) Refresh();
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
        if (valueText != null) valueText.text = Sensitivity.Format(Current);
    }
}

/// <summary>Binds a cloned slider to the "Button prompts" setting, one step per choice.</summary>
internal sealed class ButtonPromptsRow : MonoBehaviour, ISettingsRow
{
    private Slider slider = null!;
    private TMP_Text? valueText;
    private ConfigEntry<PromptStyle> entry = null!;

    public ButtonPromptsRow Init(Slider slider, TMP_Text? valueText, ConfigEntry<PromptStyle> entry)
    {
        this.slider = slider;
        this.valueText = valueText;
        this.entry = entry;
        slider.onValueChanged.AddListener(OnSliderChanged);
        return this;
    }

    private void OnEnable()
    {
        if (slider != null) Refresh();
    }

    public void Refresh()
    {
        slider.SetValueWithoutNotify(PromptStyles.ToStep(entry.Value));
        UpdateText();
    }

    private void OnSliderChanged(float step)
    {
        entry.Value = PromptStyles.FromStep((int)step);
        UpdateText();
    }

    private void UpdateText()
    {
        if (valueText != null) valueText.text = PromptStyles.Label(entry.Value);
    }
}
