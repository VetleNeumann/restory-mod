using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Infrastructure.CommonServices;
using Restory.UserInterface;
using Rewired.Components;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.CursorSpeed;

/// <summary>
/// Gamepad cursor speed. The game sets PlayerMouse.pointerSpeed = 1 whenever input switches to
/// the gamepad; we override it with the configured multiplier, and re-apply live on change.
/// </summary>
internal static class CursorSpeedFeature
{
    public static ConfigEntry<float> Speed = null!;

    public static void Bind(ConfigFile config)
    {
        Speed = config.Bind(
            "Controller",
            "CursorSpeed",
            Core.CursorSpeed.Default,
            new ConfigDescription(
                "Gamepad cursor speed multiplier. 1 = game default.",
                new AcceptableValueRange<float>(Core.CursorSpeed.Min, Core.CursorSpeed.Max)));

        Speed.SettingChanged += (_, _) => ReapplyToActiveCursors();
    }

    public static float Current => Core.CursorSpeed.Normalize(Speed.Value);

    /// <summary>Re-runs the game's handler on every live cursor so our postfix sets the new speed.</summary>
    private static void ReapplyToActiveCursors()
    {
        var handler = GameTargets.Method(PatchTargets.CursorControlsTypeChanged);
        var controlsManagerField = GameTargets.Field(PatchTargets.CursorControlsManager);
        if (handler is null || controlsManagerField is null) return;

        foreach (var view in Object.FindObjectsByType<VirtualCursorView>(FindObjectsSortMode.None))
        {
            if (controlsManagerField.GetValue(view) is ControlsManager controlsManager && controlsManager)
                handler.Invoke(view, new object[] { controlsManager.ControlType });
        }
    }
}

[HarmonyPatch]
internal static class CursorSpeedPatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.CursorControlsTypeChanged,
        PatchTargets.CursorPlayerMouse);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.CursorControlsTypeChanged)!;

    private static void Postfix(InputControlsType type, PlayerMouse ___playerMouse)
    {
        if (type == InputControlsType.Joystick && ___playerMouse != null)
            ___playerMouse.pointerSpeed = CursorSpeedFeature.Current;
    }
}
