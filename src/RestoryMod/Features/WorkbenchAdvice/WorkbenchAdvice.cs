using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Workplace;
using Restory.Infrastructure.CommonServices;
using Restory.UI.Presenters.Controllers;
using Restory.UserInterface;
using Restory.UserInterface.CommonElements;
using RestoryMod.Core;
using RestoryMod.Features.DropHint;
using RestoryMod.Features.Fade;
using RestoryMod.Features.Notepad;
using RestoryMod.Features.ZoomToggle;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Object = UnityEngine.Object;
using UIAction = RewiredConsts.Action.UI;

namespace RestoryMod.Features.WorkbenchAdvice;

internal sealed class WorkbenchAdviceFeature : IFeature
{
    public FeatureManifest Manifest => FeatureManifests.WorkbenchAdvice;

    public void Load(FeatureContext ctx)
    {
        ctx.Patch(typeof(WorkbenchAdvicePatch));
        WorkbenchAdvicePatch.Resume();
    }
}

/// <summary>
/// The controls sign at the workbench. The game lists its zoom triggers as R2 then L2 (zoom in, then
/// out); we put L2 first, as the pad has them, and show L2 alone while the zoom toggle is on, since R2
/// no longer zooms. The rotation prompt names the stick the device spins with: the game's names the
/// left stick, which moves the cursor. We also add rows for the mod's workbench buttons: the L1 fade, the
/// R2 hint, and the Notepad and inventory buttons. Those rows are for the gamepad only and show while
/// their feature is on.
/// </summary>
[HarmonyPatch]
internal static class WorkbenchAdvicePatch
{
    private static readonly AccessTools.FieldRef<WorkSurface, GameObject> Sign =
        AccessTools.FieldRefAccess<WorkSurface, GameObject>(PatchTargets.WorkSurfaceAdviceSign.MemberName);

    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.WorkbenchAdvice);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.WorkSurfaceToggleAdvices)!;

    private static void Postfix(WorkSurface __instance) => Apply(__instance);

    /// <summary>After a hot reload: the sign was set up before this copy loaded. On a normal start there's no bench yet.</summary>
    public static void Resume()
    {
        if (!Prepare()) return;
        var surface = Object.FindAnyObjectByType<WorkSurface>();
        if (surface) Apply(surface);
    }

    private static void Apply(WorkSurface surface)
    {
        try
        {
            var sign = Sign(surface);
            // Set up once per sign; a new scene brings a new sign.
            if (!sign || sign.GetComponentInChildren<AdviceRows>(true)) return;
            AdviceRows.Build(surface, sign);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Workbench advice: {e}");
        }
    }
}

/// <summary>
/// Lives on the sign's row list and keeps our changes to it current: which rows show follows the controls
/// in use and the mod's settings, checked each frame the sign is up.
/// </summary>
internal sealed class AdviceRows : MonoBehaviour
{
    private readonly List<(GameObject Root, Func<bool> Wanted)> rows = new();
    private ControlsManager controls = null!;
    private GUI_InputActionElement zoomIn = null!;
    private Transform zoomInHome = null!;
    private GUI_InputActionElement rotation = null!;

    public static void Build(WorkSurface surface, GameObject sign)
    {
        var prompts = sign.GetComponentsInChildren<GUI_InputActionElement>(true);
        var zoom = prompts.Where(p => p.ActionId == RewiredConsts.Action.CameraControl.Camera_Zoom).ToArray();
        var zoomIn = zoom.FirstOrDefault(p => p.AxisRange == AxisRange.Positive);
        var zoomOut = zoom.FirstOrDefault(p => p.AxisRange == AxisRange.Negative);
        // The rotation row (stick glyph, mouse glyph, label) is the template for ours.
        var rotation = prompts.FirstOrDefault(p => p.ActionId == UIAction.UIMouseHorizontal);
        if (!zoomIn || !zoomOut || zoomIn!.transform.parent != zoomOut!.transform.parent || !rotation)
        {
            Plugin.Logger.LogWarning("Workbench advice: the sign's zoom or rotation prompts were not found.");
            return;
        }
        var template = rotation!.transform.parent;
        var list = template.parent;

        // The game's own prompts were injected by the bench's scene; our copies need the same container.
        var context = Object.FindObjectsByType<SceneContext>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.gameObject.scene == surface.gameObject.scene);
        if (!context)
        {
            Plugin.Logger.LogWarning("Workbench advice: no Zenject context for the workbench scene.");
            return;
        }
        var container = context!.Container;

        var self = list.gameObject.AddComponent<AdviceRows>();
        self.controls = container.Resolve<ControlsManager>();
        self.zoomIn = zoomIn;
        self.rotation = rotation;
        Plugin.Teardown.Add(self.Undo);

        // L2 before R2, as on the pad. R2 goes back in front of L2 on unload.
        if (zoomOut.transform.GetSiblingIndex() > zoomIn.transform.GetSiblingIndex())
        {
            zoomOut.transform.SetSiblingIndex(zoomIn.transform.GetSiblingIndex());
            self.zoomInHome = zoomOut.transform;
        }

        // The device spins by Camera Rotate Horizontal/Vertical: the right stick.
        rotation.SetActionIdAndAxisRange(RewiredConsts.Action.CameraControl.Camera_Rotate_Horizontal, rotation.AxisRange);

        // Below the game's rotation and zoom: the rest of looking at the device, then help, then the screens.
        self.Add(container, template, list, "Fade device", UIAction.UI_LB,
            () => DeviceFadeFeature.Enabled.Value && DeviceFadeFeature.TargetsPresentCached);
        self.Add(container, template, list, "Hint", UIAction.UI_RT,
            () => ScrewHintButtonFeature.Mode.Value != HintButtonMode.Off && ScrewHintButtonFeature.TargetsPresent());
        self.Add(container, template, list, "Notepad", RewiredConsts.Action.Copybook.OpenCopybook,
            () => NotepadKeyFeature.Enabled.Value);
        self.Add(container, template, list, "Inventory", RewiredConsts.Action.Inventory.Inventory0, () => true);
        self.Refresh();
    }

    private void Add(DiContainer container, Transform template, Transform list, string label, int actionId, Func<bool> wanted)
    {
        // Cloned under an inactive holder so nothing in the copy wakes before we strip and inject it.
        var holder = new GameObject("RestoryMod_CloneHolder");
        holder.SetActive(false);
        try
        {
            var row = Object.Instantiate(template.gameObject, holder.transform);
            row.name = $"RestoryMod_{label}";
            foreach (var component in row.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Localisation would overwrite our label; the switcher would bring back the mouse glyph.
                if (component is GUI_LocalisedText or GUI_ControlsTypeGameObjectsSwitcher)
                    Object.DestroyImmediate(component);
            }
            var prompt = row.GetComponentInChildren<GUI_InputActionElement>(true);
            // The switcher hides the pad glyph while the mouse is in use; with it gone, nothing would bring it back.
            prompt.gameObject.SetActive(true);
            // The mouse glyph: the row only ever shows on the gamepad.
            foreach (var image in row.GetComponentsInChildren<Image>(true))
                if (!image.GetComponent<GUI_InputActionElement>()) Object.DestroyImmediate(image.gameObject);
            var text = row.GetComponentInChildren<TMP_Text>(true);
            if (text) text.text = label;

            // A clone's components are new and uninjected; inject them as the game's scene load did.
            container.InjectGameObject(row);
            row.transform.SetParent(list, false);
            row.transform.localScale = Vector3.one;
            row.transform.localRotation = Quaternion.identity;
            row.SetActive(false);
            prompt.SetActionIdAndAxisRange(actionId, AxisRange.Positive);
            rows.Add((row, wanted));
        }
        finally
        {
            Object.Destroy(holder);
        }
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        try
        {
            var gamepad = controls.ControlType == InputControlsType.Joystick;
            foreach (var row in rows)
            {
                var show = gamepad && row.Wanted();
                if (row.Root && row.Root.activeSelf != show) row.Root.SetActive(show);
            }
            var showZoomIn = !(ZoomToggleFeature.Enabled.Value && ZoomToggleFeature.TargetsPresent());
            if (zoomIn && zoomIn.gameObject.activeSelf != showZoomIn) zoomIn.gameObject.SetActive(showZoomIn);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Workbench advice: {e}");
            enabled = false;
        }
    }

    /// <summary>Back to the game's sign: our rows gone, R2 shown and in front of L2 again, the left stick for rotation.</summary>
    private void Undo()
    {
        foreach (var row in rows)
        {
            if (!row.Root) continue;
            // Destroy waits for the end of the frame; renamed, the row can't be mistaken for ours by then.
            row.Root.name += "_Removed";
            Object.Destroy(row.Root);
        }
        if (zoomIn)
        {
            zoomIn.gameObject.SetActive(true);
            if (zoomInHome) zoomIn.transform.SetSiblingIndex(zoomInHome.GetSiblingIndex());
        }
        if (rotation) rotation.SetActionIdAndAxisRange(UIAction.UIMouseHorizontal, rotation.AxisRange);
        if (this) Destroy(this);
    }
}
