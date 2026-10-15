using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Equipment;
using Restory.Gameplay.OverlayActivators;
using Restory.Gameplay.PlayerInput;
using Restory.UI.Presenters.Notepad;
using Restory.UI.Presenters.PauseMenu;
using RestoryMod.Core;
using Rewired;

namespace RestoryMod.Features.Notepad;

internal static class NotepadKeyFeature
{
    /// <summary>Checked on each key press, so it can be switched live from the config file.</summary>
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Notepad", "KeyTogglesAtWorkbench", true,
            "At the workbench, the Notepad key (Square / X on a gamepad, J on a keyboard) switches the " +
            "Notepad between peeking at the screen edge and fully open. The game ignores the key there.");
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Notepad key failed (further errors suppressed): {e}");
    }
}

/// <summary>
/// Listens for the Notepad key next to the game's own listener. The game's handler toggles the
/// Notepad window on release but is blocked at the workbench; ours acts on press, and only while the
/// Notepad is the sliding workbench panel, so the two never both act on one press.
/// The sticky Open reuses the game's own hotkeyPressed flag, which UpdateViewState already treats as
/// "hold Open" and which pointer enter/exit leaves alone.
/// </summary>
[HarmonyPatch]
internal static class NotepadKeyListenPatch
{
    private static IPlayerInput? playerInput;
    private static GUI_NotepadWindow? window;
    private static NotepadInteractiveWorkplaceItem? item;
    private static GUI_PauseMenu? pauseMenu;
    private static AccessTools.FieldRef<GUI_NotepadWindow, bool>? hotkeyPressed;
    private static Action<GUI_NotepadWindow>? updateViewState;

    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.NotepadActivatorInitialize,
        PatchTargets.NotepadActivatorDispose,
        PatchTargets.NotepadActivatorPlayerInput,
        PatchTargets.NotepadActivatorWindow,
        PatchTargets.NotepadActivatorItem,
        PatchTargets.NotepadActivatorPauseMenu,
        PatchTargets.NotepadWindowHotkeyPressed,
        PatchTargets.NotepadWindowUpdateViewState);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.NotepadActivatorInitialize)!;

    private static void Postfix(
        IPlayerInput ___playerInput,
        GUI_NotepadWindow ___notepadWindow,
        NotepadInteractiveWorkplaceItem ___notepadInteractiveWorkplaceItem,
        GUI_PauseMenu ___pauseMenu)
    {
        try
        {
            hotkeyPressed ??= AccessTools.FieldRefAccess<GUI_NotepadWindow, bool>(
                PatchTargets.NotepadWindowHotkeyPressed.MemberName!);
            updateViewState ??= AccessTools.MethodDelegate<Action<GUI_NotepadWindow>>(
                GameTargets.Method(PatchTargets.NotepadWindowUpdateViewState)!);

            Unsubscribe();
            playerInput = ___playerInput;
            window = ___notepadWindow;
            item = ___notepadInteractiveWorkplaceItem;
            pauseMenu = ___pauseMenu;
            playerInput.AddInputEventDelegate(OnNotepadKeyPressed, InputActionEventType.ButtonJustPressed,
                NotepadKeyActionId);
        }
        catch (Exception e)
        {
            NotepadKeyFeature.LogOnce(e);
        }
    }

    /// <summary>The game's OpenCopybook action; it hardcodes the same id.</summary>
    private const int NotepadKeyActionId = 90;

    public static void Unsubscribe()
    {
        playerInput?.RemoveInputEventDelegate(OnNotepadKeyPressed, InputActionEventType.ButtonJustPressed,
            NotepadKeyActionId);
        playerInput = null;
        window = null;
        item = null;
        pauseMenu = null;
    }

    private static void OnNotepadKeyPressed(InputActionEventData _)
    {
        try
        {
            if (!NotepadKeyFeature.Enabled.Value) return;
            if (!window || !item || hotkeyPressed is null || updateViewState is null) return;
            if (pauseMenu && pauseMenu!.IsShown) return;

            ref var keyOpen = ref hotkeyPressed(window!);
            var next = NotepadToggle.AfterKeyPress(window!.IsVisible, window.IsSlidingMode, item!.WindowIsPinned, keyOpen);
            if (next == keyOpen) return;

            keyOpen = next;
            updateViewState(window);
        }
        catch (Exception e)
        {
            NotepadKeyFeature.LogOnce(e);
        }
    }
}

/// <summary>Drops the listener with the scene, as the game does with its own.</summary>
[HarmonyPatch]
internal static class NotepadKeyUnlistenPatch
{
    private static bool Prepare() => GameTargets.AllPresent(PatchTargets.NotepadActivatorDispose);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.NotepadActivatorDispose)!;

    private static void Postfix() => NotepadKeyListenPatch.Unsubscribe();
}

/// <summary>
/// Ends key open when the Notepad is pinned (so unpinning returns to Peeking) or the workbench
/// session ends. Every such change passes through UpdateViewState.
/// </summary>
[HarmonyPatch]
internal static class NotepadKeyResetPatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.NotepadWindowUpdateViewState,
        PatchTargets.NotepadWindowHotkeyPressed,
        PatchTargets.NotepadWindowIsSlidingMode,
        PatchTargets.NotepadWindowItem);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.NotepadWindowUpdateViewState)!;

    private static void Prefix(
        ref bool ___hotkeyPressed,
        bool ___isSlidingMode,
        NotepadInteractiveWorkplaceItem ___notepadInteractiveWorkplaceItem)
    {
        if (!___hotkeyPressed || !___notepadInteractiveWorkplaceItem) return;
        ___hotkeyPressed = NotepadToggle.KeepKeyOpen(___isSlidingMode,
            ___notepadInteractiveWorkplaceItem.WindowIsPinned, ___hotkeyPressed);
    }
}
