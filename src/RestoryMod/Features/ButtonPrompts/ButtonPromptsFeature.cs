using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using IAmFuture.Data.Controllers;
using Restory.Gameplay.GameSettings;
using Restory.UI.Presenters.Controllers;
using RestoryMod.Core;
using Rewired;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.ButtonPrompts;

/// <summary>
/// Gamepad button prompts that match the pad in hand. The game picks its glyph set from Rewired's
/// hardware id, but under Steam Input every pad reaches Rewired as an Xbox 360 pad. We ask Steam for
/// the real pad and point the game's own glyph lookup at the matching set; the "Button prompts"
/// setting can also pin one set. The game draws every set but PlayStation with the same generic art,
/// so <see cref="PlayStationArt"/> supplies the PlayStation one.
/// </summary>
internal static class ButtonPromptsFeature
{
    public static ConfigEntry<PromptStyle> Style = null!;
    public static ConfigEntry<bool> DetectThroughSteamInput = null!;

    public static void Bind(ConfigFile config)
    {
        Style = config.Bind(
            "Controller",
            "ButtonPrompts",
            PromptStyle.Auto,
            "Which button art gamepad prompts show. Auto shows PlayStation art for PlayStation pads and the game's generic A/B/X/Y art for any other.");
        DetectThroughSteamInput = config.Bind(
            "Controller",
            "DetectThroughSteamInput",
            true,
            "Ask Steam which pad is behind Steam Input (which makes every pad look like an Xbox pad to the game). " +
            "Turn off if controller input misbehaves; prompts then follow what the game itself detects.");
        Style.SettingChanged += (_, _) => RefreshPrompts();
        DetectThroughSteamInput.SettingChanged += (_, _) => RefreshPrompts();
    }

    public static bool TargetsPresent() => GameTargets.AllPresent(
        PatchTargets.PromptGetControllerId,
        PatchTargets.PromptUpdateData,
        PatchTargets.PromptControllerIdsList,
        PatchTargets.PromptControllerIdsMap,
        PatchTargets.PromptGameSettings,
        PatchTargets.ControllerIdsMapLookup,
        PatchTargets.ControllerIdsLookup,
        PatchTargets.ControllerIdsDefaultGamepad,
        PatchTargets.GameSettingsGamepadScheme);

    /// <summary>
    /// What kind of pad this joystick is, preferring Steam's report over Rewired's. Steam's report is for
    /// the first pad it handles, not necessarily this one: the game is single-player with one pad per player.
    /// </summary>
    public static PadKind Detect(Controller joystick) =>
        PadKinds.Resolve(SteamPads.ActiveTypeName(), joystick.hardwareTypeGuid);

    /// <summary>Redraws every active prompt; inactive ones redraw themselves when enabled.</summary>
    public static void RefreshPrompts()
    {
        // The poller only runs when the game's prompt types resolved; without them, Redraw can't even load.
        if (!ButtonPromptsPoller.Running) return;
        try
        {
            Redraw();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Button prompts refresh: {e}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Redraw()
    {
        foreach (var prompt in Object.FindObjectsByType<GUI_InputActionElement>(FindObjectsSortMode.None))
            prompt.UpdateData();
    }
}

[HarmonyPatch]
internal static class ButtonPromptsPatch
{
    private static bool Prepare() => ButtonPromptsFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.PromptGetControllerId)!;

    private static void Postfix(
        Controller controller,
        ref ControllerId controllerId,
        ref bool __result,
        ControllerIdsList ___controllerIdsList,
        RewiredControllerIdsDependencyMap ___rewiredControllerIdsDependencyMap,
        GameSettingsManager ___gameSettingsManager)
    {
        try
        {
            if (controller is not { type: ControllerType.Joystick } || !___controllerIdsList) return;

            var style = ButtonPromptsFeature.Style.Value;
            // The game's own scheme override (unused by the game today) wins over our detection, not over the setting.
            if (style == PromptStyle.Auto && ___gameSettingsManager &&
                ___controllerIdsList.TryGetControllerId(___gameSettingsManager.GamepadScheme, out _)) return;

            var art = PadKinds.ArtToShow(style, ButtonPromptsFeature.Detect(controller));
            if (art == PadKind.Unknown) return;

            ControllerId? chosen = null;
            if (PadKinds.GameMapGuid(art) is { } guid && ___rewiredControllerIdsDependencyMap &&
                ___rewiredControllerIdsDependencyMap.TryGetControllerId(guid, out var mapped))
                chosen = mapped;
            if (!chosen) chosen = ___controllerIdsList.DefaultGamepadId;
            if (!chosen) return;

            controllerId = chosen!;
            __result = true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Button prompts: {e}");
        }
    }
}

/// <summary>
/// Notices when the pad's kind changes (another pad, Steam Input toggled, Steam answering only some
/// frames after start-up) and redraws the prompts on screen. The game only redraws them itself when
/// switching between mouse and gamepad. Also loads the PlayStation art once it is wanted, so no prompt
/// draw has to.
/// </summary>
internal sealed class ButtonPromptsPoller : MonoBehaviour
{
    private const float IntervalSeconds = 1f;

    public static bool Running { get; private set; }

    /// <summary>The pad last seen, Unknown until one is.</summary>
    public static PadKind Detected { get; private set; }

    /// <summary>Raised when <see cref="Detected"/> changes.</summary>
    public static event Action? DetectedChanged;

    private float nextCheck;
    private PadKind? lastKind;
    private string? lastSteamType;

    private void Awake() => Running = true;

    private void OnDestroy() => Running = false;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + IntervalSeconds;

        try
        {
            if (!ReInput.isReady) return;
            var joystick = ActiveJoystick();
            if (joystick is null) return;

            var steamType = SteamPads.ActiveTypeName();
            var kind = ButtonPromptsFeature.Detect(joystick);
            if (PadKinds.ArtToShow(ButtonPromptsFeature.Style.Value, kind) == PadKind.PlayStation &&
                PlayStationArt.Load())
                ButtonPromptsFeature.RefreshPrompts();
            if (kind == lastKind && steamType == lastSteamType) return;

            Plugin.Logger.LogInfo($"Gamepad: {kind} (\"{joystick.hardwareName}\", Steam Input: {steamType ?? "not handling it"}).");
            lastKind = kind;
            lastSteamType = steamType;
            // Also on the first sighting: prompts drawn before Steam answered went by Rewired's view.
            ButtonPromptsFeature.RefreshPrompts();
            if (Detected != kind)
            {
                Detected = kind;
                DetectedChanged?.Invoke();
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Button prompts poll: {e}");
            enabled = false;
        }
    }

    private static Joystick? ActiveJoystick()
    {
        if (ReInput.controllers.GetLastActiveController() is Joystick active) return active;
        var joysticks = ReInput.controllers.Joysticks;
        return joysticks.Count > 0 ? joysticks[0] : null;
    }
}
