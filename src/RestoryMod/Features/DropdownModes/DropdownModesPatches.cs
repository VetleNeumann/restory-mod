using System.Reflection;
using HarmonyLib;
using RestoryMod.Core;
using TMPro;
using UnityEngine.EventSystems;

namespace RestoryMod.Features.DropdownModes;

/// <summary>Remembers whether a dropdown is being opened by submit (nav mode) or by a click (pointer mode).</summary>
[HarmonyPatch]
internal static class DropdownOpenPatch
{
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
    private static bool Prepare() => GameTargets.AllPresent(PatchTargets.UiInputProcess);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputProcess)!;

    private static void Prefix(CustomRewiredStandaloneInputModule __instance) =>
        DropdownModesFeature.BeginFrame(new CustomRewiredStandaloneInputModuleIds(__instance));
}

/// <summary>While a dropdown is open, only the D-pad moves the highlighted row; the stick is the cursor's.</summary>
[HarmonyPatch]
internal static class DropdownStickMovePatch
{
    private static bool Prepare() => GameTargets.AllPresent(PatchTargets.UiInputSendMove);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputSendMove)!;

    private static bool Prefix(ref bool __result)
    {
        if (!DropdownModesFeature.SuppressStickMove) return true;
        __result = false;
        return false;
    }
}

/// <summary>In pointer mode, A clicks at the cursor only; its submit to the highlighted row is dropped.</summary>
[HarmonyPatch]
internal static class DropdownSubmitPatch
{
    private static bool Prepare() => GameTargets.AllPresent(PatchTargets.UiInputSendSubmit);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputSendSubmit)!;

    private static bool Prefix(CustomRewiredStandaloneInputModule __instance, ref bool __result)
    {
        if (!DropdownModesFeature.SuppressSubmit ||
            !DropdownModesFeature.SubmitThisFrame(new CustomRewiredStandaloneInputModuleIds(__instance)))
            return true;
        __result = false;
        return false;
    }
}

/// <summary>In nav mode, the virtual cursor neither hovers nor clicks.</summary>
[HarmonyPatch]
internal static class DropdownCursorClickPatch
{
    private static bool Prepare() => GameTargets.AllPresent(PatchTargets.UiInputProcessMouseEvent);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.UiInputProcessMouseEvent)!;

    private static bool Prefix() => !DropdownModesFeature.SuppressClick;
}

/// <summary>In nav mode the cursor image stays hidden until the stick moves.</summary>
[HarmonyPatch]
internal static class DropdownCursorImagePatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.CursorUpdateImageSetActive,
        PatchTargets.CursorImage);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.CursorUpdateImageSetActive)!;

    private static void Postfix(UnityEngine.UI.RawImage ___image)
    {
        if (DropdownModesFeature.CursorHidden && ___image)
            ___image.gameObject.SetActive(false);
    }
}
