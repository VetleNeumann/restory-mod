using System;
using System.Reflection;
using HarmonyLib;
using RestoryMod.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.CursorSpeed;

/// <summary>
/// Adds a "Controller sensitivity" row to the settings menu. The menu is prefab-wired, so the row
/// is cloned from the dialogue volume row and inserted right after it on the first Show().
/// </summary>
[HarmonyPatch]
internal static class CursorSpeedSliderPatch
{
    private const string RowName = "RestoryMod_ControllerSensitivity";
    private const string Label = "Controller sensitivity";

    private static bool Prepare() =>
        GameTargets.Method(PatchTargets.SettingsMenuShow) is not null &&
        GameTargets.Field(PatchTargets.SettingsMenuSoundSettingsView) is not null &&
        GameTargets.Field(PatchTargets.SoundViewDialogueVolumeSlider) is not null;

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.SettingsMenuShow)!;

    private static void Postfix(MonoBehaviour __instance)
    {
        try
        {
            var soundView = GameTargets.Field(PatchTargets.SettingsMenuSoundSettingsView)!.GetValue(__instance);
            if (GameTargets.Field(PatchTargets.SoundViewDialogueVolumeSlider)!.GetValue(soundView) is not Slider template)
            {
                Plugin.Logger.LogWarning("Controller sensitivity row: dialogue volume slider not found.");
                return;
            }

            var templateRow = template.transform.parent;
            var existing = templateRow.parent.Find(RowName);
            if (existing != null)
            {
                existing.GetComponent<ControllerSensitivityRow>()?.Refresh();
                return;
            }

            CreateRow(template, templateRow);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Controller sensitivity row failed: {e}");
        }
    }

    private static void CreateRow(Slider template, Transform templateRow)
    {
        // Clone under an inactive holder so no Awake/OnEnable runs before we strip game components.
        var holder = new GameObject("RestoryMod_CloneHolder");
        holder.SetActive(false);
        var row = Object.Instantiate(templateRow.gameObject, holder.transform);
        row.name = RowName;

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
        slider.maxValue = Core.CursorSpeed.SliderSteps;

        var label = row.transform.GetChild(0).GetComponent<TMP_Text>();
        if (label != null) label.text = Label;
        var valueText = slider.GetComponentInChildren<TMP_Text>(true);

        SpliceNavigation(template, slider);

        row.AddComponent<ControllerSensitivityRow>().Init(slider, valueText);
        row.transform.SetParent(templateRow.parent, false);
        row.transform.SetSiblingIndex(templateRow.GetSiblingIndex() + 1);
        Object.Destroy(holder);

        Plugin.Logger.LogInfo($"Controller sensitivity row added (navigation: {template.navigation.mode}).");
    }

    /// <summary>
    /// With explicit navigation the clone copies the template's links, so splice it in between the
    /// template and whatever was below it. Automatic navigation needs nothing.
    /// </summary>
    private static void SpliceNavigation(Slider above, Slider inserted)
    {
        var aboveNav = above.navigation;
        if (aboveNav.mode != Navigation.Mode.Explicit) return;

        var below = aboveNav.selectOnDown;

        var insertedNav = aboveNav;
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

/// <summary>Binds the cloned slider to the CursorSpeed config entry.</summary>
internal sealed class ControllerSensitivityRow : MonoBehaviour
{
    private Slider slider = null!;
    private TMP_Text? valueText;

    public void Init(Slider slider, TMP_Text? valueText)
    {
        this.slider = slider;
        this.valueText = valueText;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnEnable()
    {
        if (slider != null) Refresh();
    }

    /// <summary>Pulls the current config value into the slider (it may have been edited by hand).</summary>
    public void Refresh()
    {
        slider.SetValueWithoutNotify(Core.CursorSpeed.ToStep(CursorSpeedFeature.Current));
        UpdateText();
    }

    private void OnSliderChanged(float step)
    {
        CursorSpeedFeature.Speed.Value = Core.CursorSpeed.FromStep((int)step);
        UpdateText();
    }

    private void UpdateText()
    {
        if (valueText != null) valueText.text = Core.CursorSpeed.Format(CursorSpeedFeature.Current);
    }
}
