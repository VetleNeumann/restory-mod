using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.UserInterface;
using RestoryMod.Core;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlayerMouse = Rewired.Components.PlayerMouse;

namespace RestoryMod.Features.DropdownModes;

/// <summary>
/// The gamepad's A button is bound to both UI submit and the virtual cursor's left click, and the
/// left stick to both UI navigation and the cursor. In an open dropdown that means A picks two rows
/// and the stick moves the highlight and the cursor at once. This keeps one of the two in charge:
/// pointer mode (cursor) or nav mode (D-pad), per <see cref="Core.DropdownModes"/>.
/// </summary>
internal static class DropdownModesFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    private static readonly Core.DropdownModes Modes = new();
    private static readonly AccessTools.FieldRef<TMP_Dropdown, GameObject> DropdownList =
        AccessTools.FieldRefAccess<TMP_Dropdown, GameObject>("m_Dropdown");
    private static readonly List<RaycastResult> RaycastResults = new();

    private static TMP_Dropdown? openDropdown;
    private static DropdownMode? pendingOpener;
    private static Player? player;
    private static PlayerMouse? playerMouse;
    private static Vector2 lastCursorPosition;
    private static bool dpadHeld;
    private static bool cursorWasHidden;

    /// <summary>A gamepad is driving an open dropdown through the virtual cursor; gates every patch.</summary>
    public static bool Active { get; private set; }

    public static bool SuppressClick => Active && Modes.SuppressClick;
    public static bool SuppressSubmit => Active && Modes.SuppressSubmit;
    public static bool SuppressStickMove => Active && !dpadHeld;
    public static bool CursorHidden => Active && Modes.CursorHidden;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Controller", "DropdownModes", true,
            "In an open dropdown, the gamepad drives either the cursor (stick, A clicks) or the " +
            "highlighted row (D-pad, A picks it), never both at once.");
        Enabled.SettingChanged += (_, _) => Refresh();
    }

    public static void NoteOpener(DropdownMode opener) => pendingOpener = opener;
    public static void ClearOpener() => pendingOpener = null;

    public static void Opened(TMP_Dropdown dropdown)
    {
        if (dropdown == openDropdown || !dropdown.IsExpanded) return;
        openDropdown = dropdown;
        var initial = pendingOpener ?? DropdownMode.Pointer;
        if (FindPlayerMouse() is { } mouse) lastCursorPosition = mouse.screenPosition;
        Modes.Open(initial, RowUnderCursor());
        Plugin.Logger.LogDebug($"Dropdown '{dropdown.name}' opened in {initial} mode.");

        // Opening happens mid-frame (from a submit or click), and the rest of the frame's input
        // must already obey the new mode: a nav-mode open must not also click at the cursor.
        UpdateActive();
        Refresh();
    }

    public static void Hidden(TMP_Dropdown dropdown)
    {
        if (dropdown != openDropdown) return;
        Close();
    }

    /// <summary>Runs before the input module handles a frame, so each step below sees this frame's mode.</summary>
    public static void BeginFrame(CustomRewiredStandaloneInputModuleIds ids)
    {
        player = ids.Player;
        if (Modes.IsOpen && (!openDropdown || !openDropdown!.IsExpanded))
            Close();

        var wasActive = Active;
        UpdateActive();
        if (!Active)
        {
            dpadHeld = false;
            if (wasActive) Refresh();
            return;
        }

        var row = RowUnderCursor();
        var wasDpadHeld = dpadHeld;
        dpadHeld = IsDpadSource(ids.HorizontalActionId) || IsDpadSource(ids.VerticalActionId);
        if (dpadHeld && !wasDpadHeld)
            Modes.DpadPressed(row);

        var position = playerMouse!.screenPosition;
        if (position != lastCursorPosition)
        {
            lastCursorPosition = position;
            Modes.CursorMoved(row);
        }

        Refresh();
    }

    private static void UpdateActive()
    {
        var mouse = FindPlayerMouse();
        Active = Enabled.Value && Modes.IsOpen && player is not null && mouse is not null && mouse.enabled &&
                 player.controllers.GetLastActiveController()?.type == ControllerType.Joystick;
    }

    /// <summary>The submit button went down or up this frame for any of the module's players.</summary>
    public static bool SubmitThisFrame(CustomRewiredStandaloneInputModuleIds ids) =>
        ids.Player is { } player &&
        (player.GetButtonDown(ids.SubmitActionId) || player.GetButtonUp(ids.SubmitActionId));

    private static void Close()
    {
        Modes.Close();
        openDropdown = null;
        Active = false;
        dpadHeld = false;
        Refresh();
    }

    /// <summary>
    /// Applies mode side effects: on entering nav mode the row under the cursor loses its hover
    /// tint, and the cursor image follows <see cref="CursorHidden"/>.
    /// </summary>
    private static void Refresh()
    {
        var hidden = CursorHidden;
        if (hidden == cursorWasHidden) return;
        cursorWasHidden = hidden;

        if (hidden && RowObject() is { } hovered)
        {
            ExecuteEvents.ExecuteHierarchy(hovered, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerExitHandler);
        }

        var update = GameTargets.Method(PatchTargets.CursorUpdateImageSetActive);
        if (update is null) return;
        foreach (var view in Object.FindObjectsByType<VirtualCursorView>(FindObjectsSortMode.None))
            update.Invoke(view, null);
    }

    private static bool IsDpadSource(int actionId)
    {
        if (player is null || actionId < 0) return false;
        foreach (var source in player.GetCurrentInputSources(actionId))
        {
            if (source.controllerType == ControllerType.Joystick &&
                source.actionElementMap.elementType == ControllerElementType.Button)
                return true;
        }
        return false;
    }

    private static PlayerMouse? FindPlayerMouse()
    {
        if (!playerMouse) playerMouse = Object.FindAnyObjectByType<PlayerMouse>();
        return playerMouse ? playerMouse : null;
    }

    /// <summary>Instance id of the open list's row under the virtual cursor, or null.</summary>
    private static int? RowUnderCursor()
    {
        return RowObject() is { } row ? row.GetInstanceID() : null;
    }

    private static GameObject? RowObject()
    {
        var mouse = FindPlayerMouse();
        if (!openDropdown || !mouse || !EventSystem.current) return null;
        var list = DropdownList(openDropdown!);
        if (!list) return null;

        var pointer = new PointerEventData(EventSystem.current) { position = mouse!.screenPosition };
        EventSystem.current.RaycastAll(pointer, RaycastResults);
        if (RaycastResults.Count == 0) return null;
        var hit = RaycastResults[0].gameObject;
        RaycastResults.Clear();

        var toggle = hit ? hit.GetComponentInParent<Toggle>() : null;
        return toggle && toggle!.transform.IsChildOf(list.transform) ? toggle.gameObject : null;
    }
}

/// <summary>The input module's player and action ids, read once per call site.</summary>
internal readonly struct CustomRewiredStandaloneInputModuleIds
{
    public readonly Player? Player;
    public readonly int HorizontalActionId;
    public readonly int VerticalActionId;
    public readonly int SubmitActionId;

    public CustomRewiredStandaloneInputModuleIds(CustomRewiredStandaloneInputModule module)
    {
        var ids = module.RewiredPlayerIds;
        Player = ReInput.isReady && ids is { Length: > 0 } ? ReInput.players.GetPlayer(ids[0]) : null;
        HorizontalActionId = module.HorizontalActionId;
        VerticalActionId = module.VerticalActionId;
        SubmitActionId = module.SubmitActionId;
    }
}
