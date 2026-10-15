using System.Reflection;
using HarmonyLib;
using Restory.UserInterface;
using RestoryMod.Core;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoryMod.Features.DropdownModes;

/// <summary>Remembers whether a dropdown is being opened by submit (nav mode) or by a click (pointer mode).</summary>
[HarmonyPatch]
internal static class DropdownOpenPatch
{
    private static bool Prepare() => DropdownModesFeature.TargetsPresent();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.OnSubmit))]
    private static void BeforeSubmit() => DropdownModesFeature.NoteOpener(DropdownMode.Nav);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.OnPointerClick))]
    private static void BeforeClick() => DropdownModesFeature.NoteOpener(DropdownMode.Pointer);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.OnSubmit))]
    [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.OnPointerClick))]
    private static void AfterOpen() => DropdownModesFeature.ClearOpener();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.Show))]
    private static void AfterShow(TMP_Dropdown __instance) => DropdownModesFeature.Opened(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.Hide))]
    private static void AfterHide(TMP_Dropdown __instance) => DropdownModesFeature.Hidden(__instance);
}

/// <summary>Works out the frame's mode before the input module sends any event.</summary>
[HarmonyPatch]
internal static class DropdownFramePatch
{
    private static bool Prepare() => DropdownModesFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputProcess)!;

    private static void Prefix(CustomRewiredStandaloneInputModule __instance) =>
        DropdownModesFeature.BeginFrame(__instance);
}

/// <summary>
/// While a dropdown is open, only the D-pad moves the highlighted row, and the list scrolls to keep
/// it in view; the stick belongs to the cursor.
/// </summary>
[HarmonyPatch]
internal static class DropdownStickMovePatch
{
    private static bool Prepare() => DropdownModesFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputSendMove)!;

    private static bool Prefix(ref bool __result)
    {
        if (!DropdownModesFeature.SuppressStickMove) return true;
        __result = false;
        return false;
    }

    private static void Postfix()
    {
        if (DropdownModesFeature.InNavMode) DropdownModesFeature.ScrollHighlightIntoView();
    }
}

/// <summary>
/// In pointer mode, A clicks at the cursor only: its submit to the highlighted row is dropped, as is
/// its submit to a closed dropdown that still holds the selection.
/// </summary>
[HarmonyPatch]
internal static class DropdownSubmitPatch
{
    private static bool Prepare() => DropdownModesFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputSendSubmit)!;

    private static bool Prefix(ref bool __result)
    {
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (!DropdownModesFeature.SuppressSubmit && !DropdownModesFeature.SuppressSubmitToHeader(selected))
            return true;
        __result = false;
        return false;
    }
}

/// <summary>In nav mode the virtual cursor neither hovers nor clicks; the right stick still scrolls.</summary>
[HarmonyPatch]
internal static class DropdownCursorClickPatch
{
    private static bool Prepare() => DropdownModesFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputProcessMouseEvent)!;

    private static bool Prefix()
    {
        if (!DropdownModesFeature.SuppressClick) return true;
        DropdownModesFeature.ForwardScroll();
        return false;
    }
}

/// <summary>Registers cursor views as they draw, and keeps the image hidden in nav mode until the stick moves.</summary>
[HarmonyPatch]
internal static class DropdownCursorImagePatch
{
    private static bool Prepare() => DropdownModesFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.CursorUpdateImageSetActive)!;

    private static void Postfix(VirtualCursorView __instance, RawImage ___image)
    {
        DropdownModesFeature.RegisterCursor(__instance);
        if (DropdownModesFeature.CursorHidden && ___image)
            ___image.gameObject.SetActive(false);
    }
}
