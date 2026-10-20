using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using Restory.UserInterface;
using RestoryMod.Core;
using Rewired;
using Rewired.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlayerMouse = Rewired.Components.PlayerMouse;

namespace RestoryMod.Features.DropdownModes;

/// <summary>
/// The gamepad's A button is bound to both UI submit and the virtual cursor's left click, and the
/// left stick to both UI navigation and the cursor. In an open dropdown that meant A picked two rows
/// and the stick moved the highlighted row and the cursor at once. This keeps one of the two in
/// charge: pointer mode or nav mode, per <see cref="Core.DropdownModes"/>.
/// </summary>
internal static class DropdownModesFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    private static readonly Core.DropdownModes Modes = new();
    private static readonly HashSet<VirtualCursorView> CursorViews = new();
    private static readonly List<RaycastResult> RaycastResults = new();

    private static FieldInfo? dropdownListField;
    private static FieldInfo? cursorPlayerMouseField;
    private static MethodInfo? cursorUpdateImage;

    private static UiInput input;
    private static TMP_Dropdown? openDropdown;
    private static DropdownMode? pendingOpener;
    private static Vector2 lastCursorPosition;
    private static bool cursorWasHidden;

    /// <summary>The gamepad is driving an unlocked virtual cursor; nothing here applies otherwise.</summary>
    private static bool gamepadCursor;

    public static bool SuppressClick => gamepadCursor && Modes.SuppressClick;
    public static bool SuppressSubmit => gamepadCursor && Modes.SuppressSubmit && input.SubmitDownOrUp;
    public static bool SuppressStickMove => gamepadCursor && Modes.SuppressStickMove;
    public static bool CursorHidden => gamepadCursor && Modes.CursorHidden;
    public static bool InNavMode => gamepadCursor && Modes.IsOpen && Modes.Mode == DropdownMode.Nav;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Controller", "DropdownModes", true,
            "In an open dropdown, the gamepad drives either the cursor (stick moves it, A clicks) or " +
            "the highlighted row (D-pad moves it, A picks it), never both at once.");
        Enabled.SettingChanged += (_, _) => RefreshCursor();
    }

    /// <summary>Every target the feature touches; each patch class's Prepare() uses this.</summary>
    public static bool TargetsPresent()
    {
        if (!GameTargets.AllPresent(FeatureManifests.DropdownModes)) return false;

        dropdownListField ??= GameTargets.Field(PatchTargets.DropdownList);
        cursorPlayerMouseField ??= GameTargets.Field(PatchTargets.CursorPlayerMouse);
        cursorUpdateImage ??= GameTargets.Method(PatchTargets.CursorUpdateImageSetActive);
        return dropdownListField is not null && cursorPlayerMouseField is not null && cursorUpdateImage is not null;
    }

    /// <summary>
    /// Cursor views announce themselves when they draw (Awake, or a switch of controller type), so the
    /// only search is <see cref="FindCursors"/> after a hot reload.
    /// </summary>
    public static void RegisterCursor(VirtualCursorView view) => CursorViews.Add(view);

    /// <summary>On load: after a hot reload the cursors have long drawn, so look for them once.</summary>
    public static void FindCursors()
    {
        if (!TargetsPresent()) return;
        foreach (var view in Object.FindObjectsByType<VirtualCursorView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            CursorViews.Add(view);
    }

    /// <summary>On unload, once the patches are gone: the game redraws each cursor, showing any the mod hid.</summary>
    public static void Unload()
    {
        if (cursorUpdateImage is null) return;
        CursorViews.RemoveWhere(view => !view);
        foreach (var view in CursorViews)
        {
            try
            {
                cursorUpdateImage.Invoke(view, null);
            }
            catch (TargetInvocationException e)
            {
                Plugin.Logger.LogWarning($"Cursor redraw on unload failed: {e.InnerException}");
            }
        }
    }

    public static void NoteOpener(DropdownMode opener) => pendingOpener = opener;
    public static void ClearOpener() => pendingOpener = null;

    public static void Opened(TMP_Dropdown dropdown)
    {
        if (dropdown == openDropdown || !dropdown.IsExpanded) return;
        openDropdown = dropdown;
        var initial = pendingOpener ?? DropdownMode.Pointer;
        var mouse = PlayerMouse();
        if (mouse) lastCursorPosition = mouse!.screenPosition;
        Modes.Open(initial, RowUnderCursor());
        Plugin.Logger.LogDebug($"Dropdown '{dropdown.name}' opened in {initial} mode.");

        // Opening happens mid-frame (from a submit or a click); the rest of this frame's input must
        // already obey the new mode, so a nav-mode open does not also click at the cursor.
        RefreshCursor();
    }

    public static void Hidden(TMP_Dropdown dropdown)
    {
        if (dropdown == openDropdown) Close();
    }

    /// <summary>Runs before the input module handles a frame, so every step after it sees this frame's mode.</summary>
    public static void BeginFrame(CustomRewiredStandaloneInputModule module)
    {
        input = new UiInput(module);
        if (Modes.IsOpen && (!openDropdown || !openDropdown!.IsExpanded))
            Close();

        var mouse = PlayerMouse();
        gamepadCursor = Enabled.Value && input.Player is not null && mouse && mouse!.enabled &&
                        input.Player.controllers.GetLastActiveController()?.type == ControllerType.Joystick;
        if (!gamepadCursor)
        {
            RefreshCursor();
            return;
        }

        var row = RowUnderCursor();
        if (input.SubmitDown) Modes.SubmitPressed();
        if (input.SubmitUp) Modes.SubmitReleased();
        Modes.DpadHeld(input.DpadHeld, row);

        var position = mouse!.screenPosition;
        if (position != lastCursorPosition)
        {
            lastCursorPosition = position;
            Modes.CursorMoved(row);
        }

        RefreshCursor();
    }

    /// <summary>A closed dropdown keeps the selection after it hides; A must not reopen it while A is the cursor's.</summary>
    public static bool SuppressSubmitToHeader(GameObject? selected) =>
        gamepadCursor && Modes.SuppressHeaderSubmit && input.SubmitDownOrUp &&
        selected && selected!.GetComponent<TMP_Dropdown>();

    /// <summary>In nav mode the cursor's events are skipped, but the right stick still scrolls the list.</summary>
    public static void ForwardScroll()
    {
        var mouse = PlayerMouse();
        var list = OpenList();
        if (!mouse || !list || !EventSystem.current) return;
        var delta = ((IMouseInputSource)mouse!).wheelDelta;
        if (delta == Vector2.zero) return;

        var scrollRect = list!.GetComponentInChildren<ScrollRect>();
        if (!scrollRect) return;
        ExecuteEvents.Execute(scrollRect.gameObject,
            new PointerEventData(EventSystem.current) { scrollDelta = delta }, ExecuteEvents.scrollHandler);
    }

    /// <summary>After a D-pad move, scrolls the list so the highlighted row is fully visible.</summary>
    public static void ScrollHighlightIntoView()
    {
        var list = OpenList();
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (!list || !selected || !selected!.transform.IsChildOf(list!.transform)) return;

        var scrollRect = list.GetComponentInChildren<ScrollRect>();
        if (!scrollRect || !scrollRect.content) return;
        UiScroll.RowIntoView(scrollRect, (RectTransform)selected.transform);
    }

    private static void Close()
    {
        Modes.Close();
        openDropdown = null;
        RefreshCursor();
    }

    /// <summary>
    /// Shows or hides the cursor image to match <see cref="CursorHidden"/>. On hiding, the row under
    /// the cursor also loses its hover tint, since the cursor no longer hovers.
    /// </summary>
    private static void RefreshCursor()
    {
        var hidden = CursorHidden;
        if (hidden == cursorWasHidden) return;
        cursorWasHidden = hidden;

        var hovered = hidden ? RowObject() : null;
        if (hovered && EventSystem.current)
        {
            ExecuteEvents.ExecuteHierarchy(hovered, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerExitHandler);
        }

        CursorViews.RemoveWhere(view => !view);
        foreach (var view in CursorViews)
            cursorUpdateImage!.Invoke(view, null);
    }

    private static PlayerMouse? PlayerMouse()
    {
        foreach (var view in CursorViews)
        {
            if (view && cursorPlayerMouseField!.GetValue(view) is PlayerMouse mouse && mouse)
                return mouse;
        }
        return null;
    }

    private static GameObject? OpenList() =>
        openDropdown && dropdownListField!.GetValue(openDropdown) is GameObject list && list ? list : null;

    /// <summary>Instance id of the open list's row under the virtual cursor, or null.</summary>
    private static int? RowUnderCursor()
    {
        var row = RowObject();
        return row ? row!.GetInstanceID() : null;
    }

    private static GameObject? RowObject()
    {
        var list = OpenList();
        var mouse = PlayerMouse();
        if (!list || !mouse || !EventSystem.current) return null;

        var pointer = new PointerEventData(EventSystem.current) { position = mouse!.screenPosition };
        EventSystem.current.RaycastAll(pointer, RaycastResults);
        var hit = RaycastResults.Count > 0 ? RaycastResults[0].gameObject : null;
        RaycastResults.Clear();

        var toggle = hit ? hit!.GetComponentInParent<Toggle>() : null;
        return toggle && toggle!.transform.IsChildOf(list!.transform) ? toggle.gameObject : null;
    }

    /// <summary>One frame of the gamepad's UI input, read through the input module's own bindings.</summary>
    private readonly struct UiInput
    {
        public readonly Player? Player;
        public readonly bool SubmitDown;
        public readonly bool SubmitUp;
        public readonly bool DpadHeld;

        public bool SubmitDownOrUp => SubmitDown || SubmitUp;

        public UiInput(CustomRewiredStandaloneInputModule module)
        {
            var ids = module.RewiredPlayerIds;
            Player = ReInput.isReady && ids.Length > 0 ? ReInput.players.GetPlayer(ids[0]) : null;
            SubmitDown = Player is not null && Player.GetButtonDown(module.SubmitActionId);
            SubmitUp = Player is not null && Player.GetButtonUp(module.SubmitActionId);
            DpadHeld = IsDpad(Player, module.HorizontalActionId) || IsDpad(Player, module.VerticalActionId);
        }

        /// <summary>The action is held by a gamepad button (the D-pad) rather than an axis (the stick).</summary>
        private static bool IsDpad(Player? player, int actionId)
        {
            if (player is null || actionId < 0 || player.GetAxisRaw(actionId) == 0f) return false;
            foreach (var source in player.GetCurrentInputSources(actionId))
            {
                if (source.controllerType == ControllerType.Joystick &&
                    source.actionElementMap.elementType == ControllerElementType.Button)
                    return true;
            }
            return false;
        }
    }
}
