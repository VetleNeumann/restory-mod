using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using RestoryMod.Core;
using RestoryMod.Features.CursorSpeed;
using RestoryMod.Features.RotationSpeed;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.Settings;

/// <summary>
/// Adds the "Controller sensitivity" and "Rotation sensitivity" rows to the settings menu. The menu is
/// prefab-wired, so each row is cloned from the dialogue volume row and inserted below it (and below
/// each other) on the first Show().
/// </summary>
[HarmonyPatch]
internal static class SensitivitySlidersPatch
{
    private sealed class RowSpec(string name, string label, Func<ConfigEntry<float>> entry)
    {
        public string Name => name;
        public string Label => label;
        public ConfigEntry<float> Entry => entry();
    }

    private static readonly RowSpec[] Rows =
    {
        new("RestoryMod_ControllerSensitivity", "Controller sensitivity", () => CursorSpeedFeature.Speed),
        new("RestoryMod_RotationSensitivity", "Rotation sensitivity", () => RotationSpeedFeature.Speed),
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
                Plugin.Logger.LogWarning("Sensitivity rows: dialogue volume slider not found.");
                return;
            }

            var templateRow = template.transform.parent;
            Selectable above = template;
            var aboveRow = templateRow;
            foreach (var spec in Rows)
            {
                var row = templateRow.parent.Find(spec.Name);
                if (row != null)
                    row.GetComponent<SensitivityRow>()?.Refresh();
                else
                    row = CreateRow(spec, template, templateRow, above, aboveRow);

                above = row.GetComponentInChildren<Slider>(true);
                aboveRow = row;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Sensitivity rows failed: {e}");
        }
    }

    private static Transform CreateRow(RowSpec spec, Slider template, Transform templateRow, Selectable above, Transform aboveRow)
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
            slider.maxValue = Sensitivity.SliderSteps;

            var label = row.transform.GetChild(0).GetComponent<TMP_Text>();
            if (label != null) label.text = spec.Label;
            var valueText = slider.GetComponentInChildren<TMP_Text>(true);

            SpliceNavigation(above, slider);

            row.AddComponent<SensitivityRow>().Init(slider, valueText, spec.Entry);
            row.transform.SetParent(templateRow.parent, false);
            row.transform.SetSiblingIndex(aboveRow.GetSiblingIndex() + 1);

            Plugin.Logger.LogInfo($"{spec.Label} row added (navigation: {template.navigation.mode}).");
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

/// <summary>Binds a cloned slider to a sensitivity config entry.</summary>
internal sealed class SensitivityRow : MonoBehaviour
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
        if (slider != null) Refresh();
    }

    private float Current => Sensitivity.Normalize(entry.Value);

    /// <summary>Pulls the current config value into the slider (it may have been edited by hand).</summary>
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
