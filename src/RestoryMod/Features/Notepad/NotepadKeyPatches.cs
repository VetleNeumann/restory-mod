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
/// </summary>
[HarmonyPatch]
internal static class NotepadKeyListenPatch
{
    /// <summary>The game's OpenCopybook action; the game hardcodes the same id.</summary>
    private const int NotepadKeyActionId = 90;

    /// <summary>The mod's sticky Open, see <see cref="NotepadToggle"/>. One Notepad per scene.</summary>
    internal static bool KeyOpen;

    private static IPlayerInput? playerInput;
    private static GUI_NotepadWindow? window;
    private static NotepadInteractiveWorkplaceItem? item;
    private static GUI_PauseMenu? pauseMenu;
    private static Action<GUI_NotepadWindow>? updateViewState;

    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.NotepadActivatorInitialize,
        PatchTargets.NotepadActivatorPlayerInput,
        PatchTargets.NotepadActivatorWindow,
        PatchTargets.NotepadActivatorItem,
        PatchTargets.NotepadActivatorPauseMenu,
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

    internal static void Unsubscribe()
    {
        try
        {
            playerInput?.RemoveInputEventDelegate(OnNotepadKeyPressed, InputActionEventType.ButtonJustPressed,
                NotepadKeyActionId);
        }
        catch (Exception e)
        {
            NotepadKeyFeature.LogOnce(e);
        }
        playerInput = null;
        window = null;
        item = null;
        pauseMenu = null;
        KeyOpen = false;
    }

    private static void OnNotepadKeyPressed(InputActionEventData _)
    {
        try
        {
            if (!NotepadKeyFeature.Enabled.Value) return;
            if (!window || !item || updateViewState is null) return;
            if (pauseMenu && pauseMenu!.IsShown) return;

            var next = NotepadToggle.AfterKeyPress(window!.IsVisible, window.IsSlidingMode, item!.WindowIsPinned, KeyOpen);
            if (next == KeyOpen) return;

            KeyOpen = next;
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
/// Applies key open inside the game's own Open/Peeking decision. The game opens the sliding Notepad
/// while its hotkeyPressed flag is set (its hold-to-peek), so key open is lent to that flag for the
/// call only; the game's own value is restored after, since its key-up handler clears the flag on
/// release. Also ends key open when the Notepad is pinned (so unpinning returns to Peeking) or the
/// workbench session ends: every such change passes through UpdateViewState.
/// </summary>
[HarmonyPatch]
internal static class NotepadKeyHoldOpenPatch
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
        NotepadInteractiveWorkplaceItem ___notepadInteractiveWorkplaceItem,
        out bool __state)
    {
        __state = ___hotkeyPressed;
        if (!NotepadKeyListenPatch.KeyOpen) return;

        var pinned = ___notepadInteractiveWorkplaceItem && ___notepadInteractiveWorkplaceItem.WindowIsPinned;
        NotepadKeyListenPatch.KeyOpen = NotepadKeyFeature.Enabled.Value &&
            NotepadToggle.KeepKeyOpen(___isSlidingMode, pinned, keyOpen: true);
        ___hotkeyPressed |= NotepadKeyListenPatch.KeyOpen;
    }

    private static void Postfix(ref bool ___hotkeyPressed, bool __state) => ___hotkeyPressed = __state;
}
