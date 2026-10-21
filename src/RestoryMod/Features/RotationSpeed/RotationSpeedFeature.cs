using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Infrastructure.CommonServices;
using RestoryMod.Core;

namespace RestoryMod.Features.RotationSpeed;

/// <summary>
/// Gamepad rotation speed at the workbench. The game spins the device by stick tilt times a fixed
/// joystickDragRotationSpeed each frame; we scale the axes just before they are applied, after the
/// game's optional Round() snapping, so the setting changes speed only. The value is read every
/// frame, so config changes apply live.
/// </summary>
internal sealed class RotationSpeedFeature : IFeature
{
    // The game's own speed (1.0) spins far too fast on a stick; 0.4 was picked in play.
    private const float DefaultSpeed = 0.4f;

    public static ConfigEntry<float> Speed = null!;

    public FeatureManifest Manifest => FeatureManifests.RotationSpeed;

    public void Load(FeatureContext ctx)
    {
        Bind(ctx.Config);
        ctx.Patch(typeof(RotationSpeedPatch));
    }

    private static void Bind(ConfigFile config)
    {
        Speed = config.Bind(
            "Controller",
            "RotationSpeed",
            DefaultSpeed,
            new ConfigDescription(
                "Gamepad speed for spinning the device at the workbench. 1 = the game's own speed.",
                new AcceptableValueRange<float>(Sensitivity.Min, Sensitivity.Max)));
    }

    public static float Current => Sensitivity.Normalize(Speed.Value);

    public static bool TargetsPresent() => GameTargets.AllPresent(FeatureManifests.RotationSpeed);
}

[HarmonyPatch]
internal static class RotationSpeedPatch
{
    private static bool Prepare() => RotationSpeedFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.RotationApply)!;

    private static void Prefix(ref float ___xAxisRotation, ref float ___yAxisRotation, ControlsManager ___controlsManager)
    {
        if (!___controlsManager || ___controlsManager.ControlType != InputControlsType.Joystick) return;

        var speed = RotationSpeedFeature.Current;
        ___xAxisRotation *= speed;
        ___yAxisRotation *= speed;
    }
}
