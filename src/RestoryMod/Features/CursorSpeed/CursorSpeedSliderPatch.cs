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
/// is cloned from the dialogue volume row (for its slider look) and appended after the last row of
/// the list, away from the audio settings, on the first Show().
/// </summary>
[HarmonyPatch]
internal static class CursorSpeedSliderPatch
{
    private const string RowName = "RestoryMod_ControllerSensitivity";
    private const string Label = "Controller sensitivity";

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
        var list = templateRow.parent;
        if (!TryFindLastRow(list, out var lastRow, out var above))
        {
            Plugin.Logger.LogWarning("Controller sensitivity row: no selectable row found in the settings list.");
            return;
        }

        // Clone under an inactive holder so no Awake/OnEnable runs before we strip game components.
        // The holder is always destroyed; if anything throws before the row is moved out, the
        // half-built row goes with it.
        var holder = new GameObject("RestoryMod_CloneHolder");
        holder.SetActive(false);
        try
        {
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

            SpliceNavigation(above, slider);

            row.AddComponent<ControllerSensitivityRow>().Init(slider, valueText);
            row.transform.SetParent(list, false);
            row.transform.SetSiblingIndex(lastRow.GetSiblingIndex() + 1);

            Plugin.Logger.LogInfo($"Controller sensitivity row added after '{lastRow.name}' (navigation: {above.navigation.mode}).");
        }
        finally
        {
            Object.Destroy(holder);
        }
    }

    /// <summary>The last active row in the settings list that the player can select.</summary>
    private static bool TryFindLastRow(Transform list, out Transform lastRow, out Selectable selectable)
    {
        for (var i = list.childCount - 1; i >= 0; i--)
        {
            var child = list.GetChild(i);
            if (!child.gameObject.activeSelf || child.name == RowName) continue;
            var found = child.GetComponentInChildren<Selectable>();
            if (found == null) continue;
            lastRow = child;
            selectable = found;
            return true;
        }
        lastRow = null!;
        selectable = null!;
        return false;
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
