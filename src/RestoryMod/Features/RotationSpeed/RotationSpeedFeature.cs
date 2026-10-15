using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Infrastructure.CommonServices;
using RestoryMod.Core;

namespace RestoryMod.Features.RotationSpeed;

/// <summary>
/// Gamepad rotation speed at the workbench. The game spins the device by stick tilt times a fixed
/// joystickDragRotationSpeed each frame; we scale the result after it is computed. The value is read
/// every frame, so config changes apply live.
/// </summary>
internal static class RotationSpeedFeature
{
    public static ConfigEntry<float> Speed = null!;

    public static void Bind(ConfigFile config)
    {
        Speed = config.Bind(
            "Controller",
            "RotationSpeed",
            Sensitivity.Default,
            new ConfigDescription(
                "Gamepad speed for spinning the device at the workbench. 1 = game default.",
                new AcceptableValueRange<float>(Sensitivity.Min, Sensitivity.Max)));
    }

    public static float Current => Sensitivity.Normalize(Speed.Value);
}

[HarmonyPatch]
internal static class RotationSpeedPatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.RotationCalculateAxis,
        PatchTargets.RotationXAxis,
        PatchTargets.RotationYAxis,
        PatchTargets.RotationControlsManager);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.RotationCalculateAxis)!;

    private static void Postfix(ref float ___xAxisRotation, ref float ___yAxisRotation, ControlsManager ___controlsManager)
    {
        if (!___controlsManager || ___controlsManager.ControlType != InputControlsType.Joystick) return;

        var speed = RotationSpeedFeature.Current;
        ___xAxisRotation *= speed;
        ___yAxisRotation *= speed;
    }
}
