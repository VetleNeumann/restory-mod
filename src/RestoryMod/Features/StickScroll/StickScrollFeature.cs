using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.UserInterface;
using Rewired;
using RestoryMod.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoryMod.Features.StickScroll;

/// <summary>
/// Stick scroll: the right stick glides scrollable menus at a speed that follows its tilt, replacing
/// the game's arrow-key-style stepping (one jump, a 0.3 s pause, then ten jumps a second).
/// </summary>
internal static class StickScrollFeature
{
    public static ConfigEntry<bool> Enabled = null!;
    private static ConfigEntry<float> innerDeadzone = null!;
    private static ConfigEntry<float> outerDeadzone = null!;
    private static ConfigEntry<float> exponent = null!;
    private static ConfigEntry<float> speed = null!;
    private static ConfigEntry<float> smoothing = null!;
    private static ConfigEntry<bool> invert = null!;
    private static ConfigEntry<float> maxSpeed = null!;
    private static ConfigEntry<float> rampTime = null!;

    public static void Bind(ConfigFile config)
    {
        var defaults = StickScrollSettings.Default;
        const string section = "Stick scroll";

        Enabled = config.Bind(section, "Enabled", true,
            "Glide menus smoothly with the right stick. Off = the game's own step-by-step scrolling.");
        innerDeadzone = config.Bind(section, "InnerDeadzone", defaults.InnerDeadzone, new ConfigDescription(
            "Stick tilt (0-1) below which nothing scrolls.", new AcceptableValueRange<float>(0f, 0.5f)));
        outerDeadzone = config.Bind(section, "OuterDeadzone", defaults.OuterDeadzone, new ConfigDescription(
            "Stick tilt (0-1) that counts as full speed.", new AcceptableValueRange<float>(0.6f, 1f)));
        exponent = config.Bind(section, "CurveExponent", defaults.Exponent, new ConfigDescription(
            "Response curve: 1 = linear, 2 = gentle near centre, 3 = very gentle.", new AcceptableValueRange<float>(1f, 3f)));
        speed = config.Bind(section, "Speed", defaults.Speed, new ConfigDescription(
            "Speed at full tilt, in view heights per second.", new AcceptableValueRange<float>(0.75f, 3f)));
        maxSpeed = config.Bind(section, "MaxSpeed", defaults.MaxSpeed, new ConfigDescription(
            "Speed reached by holding full tilt, in view heights per second. Set equal to Speed for no ramp.",
            new AcceptableValueRange<float>(0.75f, 6f)));
        rampTime = config.Bind(section, "RampTime", defaults.RampTime, new ConfigDescription(
            "Seconds of holding full tilt to go from Speed to MaxSpeed.", new AcceptableValueRange<float>(0f, 3f)));
        smoothing = config.Bind(section, "Smoothing", defaults.Smoothing, new ConfigDescription(
            "Seconds the speed takes to follow the stick (time constant). 0 = instant.", new AcceptableValueRange<float>(0f, 0.3f)));
        invert = config.Bind(section, "Invert", defaults.Invert, "Flip the scroll direction.");

        config.SettingChanged += (_, _) => Settings = Read();
        Settings = Read();
    }

    /// <summary>Current tuning, rebuilt when the config changes rather than every frame.</summary>
    public static StickScrollSettings Settings { get; private set; } = StickScrollSettings.Default;

    private static StickScrollSettings Read() => new(
        innerDeadzone.Value, outerDeadzone.Value, exponent.Value, speed.Value, smoothing.Value, invert.Value,
        maxSpeed.Value, rampTime.Value);

    /// <summary>Feature is usable only while the game handler it replaces can be silenced.</summary>
    public static bool TargetsPresent() => GameTargets.AllPresent(
        PatchTargets.ScrollbarInputResolveScroll,
        PatchTargets.CursorPlayerMouse);
}

/// <summary>
/// Reads the right stick every frame and scrolls one menu: the one holding the selection, else the
/// one under the cursor. Nothing else: the right stick also turns the camera, so a list the player
/// is not pointing at must not scroll. The target is re-picked each time the stick is pushed from rest.
/// </summary>
internal sealed class StickScrollDriver : MonoBehaviour
{
    // The game creates a single Rewired player.
    private const int PlayerId = 0;

    private readonly List<RaycastResult> raycastResults = new();
    private StickScroller scroller = new();
    private ScrollRect? target;
    private FieldInfo? cursorPlayerMouse;
    private VirtualCursorView? cursorView;

    private void Start()
    {
        cursorPlayerMouse = GameTargets.Field(PatchTargets.CursorPlayerMouse);
    }

    private void Update()
    {
        if (!StickScrollFeature.Enabled.Value || !ReInput.isReady) return;
        var player = ReInput.players.GetPlayer(PlayerId);
        if (player is null) return;

        var settings = StickScrollFeature.Settings;
        var x = 0f;
        var y = 0f;
        if (player.controllers.GetLastActiveController()?.type == ControllerType.Joystick)
        {
            // The game's own stick scroll reads UIGamepadWheelVertical.
            x = player.GetAxisRaw(RewiredConsts.Action.UI.UIGamepadWheelHorizontal);
            y = player.GetAxisRaw(RewiredConsts.Action.UI.UIGamepadWheelVertical);
        }
        var pushed = StickScroller.IsPushed(x, y, settings);

        if (!IsScrollable(target))
        {
            if (!pushed) return;
            target = FindTarget();
            scroller = new StickScroller();
            if (!target) return;
        }

        var scrollRect = target!;
        var current = scrollRect.verticalNormalizedPosition;
        var next = scroller.Advance(current, x, y, Time.unscaledDeltaTime,
            ViewportHeight(scrollRect), scrollRect.content.rect.height, settings);
        if (next != current) scrollRect.verticalNormalizedPosition = next;

        // Glide over: pick afresh next push, in case the selection or cursor moved to another menu.
        if (!pushed && !scroller.IsMoving) target = null;
    }

    private ScrollRect? FindTarget()
    {
        var eventSystem = EventSystem.current;
        if (!eventSystem) return null;

        var selected = eventSystem.currentSelectedGameObject;
        if (selected && ScrollableAncestor(selected.transform) is { } underSelection) return underSelection;

        if (CursorScreenPosition() is { } cursor)
        {
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = cursor }, raycastResults);
            foreach (var hit in raycastResults)
            {
                if (ScrollableAncestor(hit.gameObject.transform) is { } underCursor) return underCursor;
            }
        }

        return null;
    }

    private Vector2? CursorScreenPosition()
    {
        if (cursorPlayerMouse is null) return null;
        if (!cursorView) cursorView = FindAnyObjectByType<VirtualCursorView>();
        if (!cursorView) return null;
        return cursorPlayerMouse.GetValue(cursorView) is Rewired.Components.PlayerMouse mouse && mouse ? mouse.screenPosition : null;
    }

    private static ScrollRect? ScrollableAncestor(Transform start)
    {
        foreach (var scrollRect in start.GetComponentsInParent<ScrollRect>())
        {
            if (IsScrollable(scrollRect)) return scrollRect;
        }
        return null;
    }

    private static bool IsScrollable(ScrollRect? scrollRect) =>
        scrollRect && scrollRect!.isActiveAndEnabled && scrollRect.vertical && scrollRect.content &&
        scrollRect.content.rect.height > ViewportHeight(scrollRect) + 0.5f && IsVisible(scrollRect);

    /// <summary>Menus are often faded out with a CanvasGroup rather than deactivated.</summary>
    private static bool IsVisible(ScrollRect scrollRect)
    {
        foreach (var group in scrollRect.GetComponentsInParent<CanvasGroup>())
        {
            if (group.alpha <= 0.01f) return false;
            if (group.ignoreParentGroups) break;
        }
        return true;
    }

    private static float ViewportHeight(ScrollRect scrollRect) =>
        (scrollRect.viewport ? scrollRect.viewport : (RectTransform)scrollRect.transform).rect.height;
}

/// <summary>Silences the game's step-and-repeat stick scroll while ours is on.</summary>
[HarmonyPatch]
internal static class StickScrollGameHandlerPatch
{
    private static bool Prepare() => StickScrollFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ScrollbarInputResolveScroll)!;

    private static bool Prefix() => !StickScrollFeature.Enabled.Value;
}
