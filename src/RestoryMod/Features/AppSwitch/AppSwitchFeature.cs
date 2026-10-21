using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.PC;
using Restory.UI.Presenters;
using Restory.UI.Presenters.PC.Apps.Hacking;
using RestoryMod.Core;
using RestoryMod.Features.DpadSend;
using RestoryMod.Features.ShoulderButtons;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RestoryMod.Features.AppSwitch;

/// <summary>
/// D-pad left/right on the PC close the open PC app and open its neighbour on the taskbar, as a
/// click on that taskbar button would. See CONTEXT.md: App switch.
/// </summary>
internal sealed class AppSwitchFeature : IFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public FeatureManifest Manifest => FeatureManifests.AppSwitch;

    public void Load(FeatureContext ctx)
    {
        Enabled = ctx.Config.Bind("PC", "DpadSwitchesApps", true,
            "On the PC, D-pad left/right open the previous/next app on the taskbar, wrapping at the ends. " +
            "Off while a dropdown is open, while an app installs, while typing in a field, and while a hack runs.");
        if (TargetsPresent()) ctx.AddPoller<AppSwitchPoller>();
    }

    public static bool TargetsPresent() => GameTargets.AllPresent(FeatureManifests.AppSwitch);

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"App switch failed (further errors suppressed): {e}");
    }
}

/// <summary>Reads the D-pad each frame while the PC screen shows; the screen has no per-frame update of its own.</summary>
internal sealed class AppSwitchPoller : MonoBehaviour
{
    private static AccessTools.FieldRef<GUI_PcWindowsXpToolbar, List<GUI_PcAppToolbarButton>>? appButtonsField;
    private static FieldInfo? hackStateField;

    private GUI_PcWindowsXpScreen? screen;
    private PcAppManager? manager;

    private void Update()
    {
        try
        {
            if (!AppSwitchFeature.Enabled.Value || !ReInput.isReady) return;
            var step = DpadSendPatch.PressedDirection(ReInput.players.GetPlayer(GamePlayer.Id)) switch
            {
                DpadDirection.Left => -1,
                DpadDirection.Right => 1,
                _ => 0,
            };
            if (step == 0) return;

            if (!screen) screen = FindAnyObjectByType<GUI_PcWindowsXpScreen>();
            if (!screen || !screen!.IsVisible) return;
            if (screen.CurrentState is not (PcScreenStates.Desktop or PcScreenStates.InStartMenu)) return;
            if (ShoulderButtonsFeature.IsPaused() || DropdownOpen(screen) || Typing()) return;

            if (!manager) manager = FindAnyObjectByType<PcAppManager>();
            if (!manager) return;
            var open = manager!.LaunchedApp;
            if (open && open is GUI_DeviceHackingApp hacking && HackRunning(hacking)) return;

            var buttons = TaskbarOrder(screen.Toolbar);
            var current = open ? buttons.FindIndex(b => b.AppInfo == open.AppInfo) : -1;
            var target = Core.AppSwitch.Target(buttons.Count, current, step);
            if (target < 0 || target == current) return;
            manager.LaunchPcApp(buttons[target].AppInfo);
            PcClickSound.Play();
        }
        catch (Exception e)
        {
            AppSwitchFeature.LogOnce(e);
        }
    }

    /// <summary>The taskbar's PC app buttons, left first.</summary>
    private static List<GUI_PcAppToolbarButton> TaskbarOrder(GUI_PcWindowsXpToolbar toolbar)
    {
        appButtonsField ??= AccessTools.FieldRefAccess<GUI_PcWindowsXpToolbar, List<GUI_PcAppToolbarButton>>(
            PatchTargets.PcToolbarAppButtons.MemberName!);
        return ShoulderButtonsPoller.OnScreen(appButtonsField(toolbar).Where(b => b && b.gameObject.activeInHierarchy));
    }

    /// <summary>
    /// Every gamepad button feeds the hack while it runs, popups included. Before the device connects
    /// and once it is hacked, the D-pad does nothing there, so it switches apps as anywhere else.
    /// </summary>
    private static bool HackRunning(GUI_DeviceHackingApp app)
    {
        hackStateField ??= GameTargets.Field(PatchTargets.HackingAppState);
        return hackStateField!.GetValue(app).ToString() is "Hacking" or "Delay" or "Decision";
    }

    private static bool DropdownOpen(GUI_PcWindowsXpScreen screen) =>
        screen.GetComponentsInChildren<TMP_Dropdown>().Any(d => d.IsExpanded);

    /// <summary>A text or number field has the caret, so the D-pad may be moving it.</summary>
    private static bool Typing()
    {
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (!selected) return false;
        var field = selected!.GetComponent<TMP_InputField>();
        return field && field.isFocused;
    }
}
