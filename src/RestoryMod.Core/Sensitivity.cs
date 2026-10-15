using System;
using System.Globalization;

namespace RestoryMod.Core;

/// <summary>
/// Gamepad sensitivity multiplier shared by the settings sliders (cursor speed, rotation speed):
/// a value in [<see cref="Min"/>, <see cref="Max"/>] where 1 is the game's own speed, adjusted on
/// the slider in steps of <see cref="Step"/>.
/// </summary>
public static class Sensitivity
{
    public const float Min = 0.2f;
    public const float Max = 4f;
    public const float Default = 1f;
    public const float Step = 0.1f;

    /// <summary>Number of slider steps between <see cref="Min"/> and <see cref="Max"/>.</summary>
    public static int SliderSteps { get; } = (int)Math.Round((Max - Min) / Step);

    /// <summary>Clamps to the range and snaps to the nearest <see cref="Step"/>.</summary>
    public static float Normalize(float speed)
    {
        if (float.IsNaN(speed)) return Default;
        return FromStep(ToStep(speed));
    }

    public static float FromStep(int step)
    {
        step = Math.Min(SliderSteps, Math.Max(0, step));
        return (float)Math.Round(Min + step * Step, 1);
    }

    public static int ToStep(float speed)
    {
        if (float.IsNaN(speed)) speed = Default;
        var step = (int)Math.Round((speed - Min) / Step);
        return Math.Min(SliderSteps, Math.Max(0, step));
    }

    /// <summary>One decimal, invariant culture: "1.0", "2.3".</summary>
    public static string Format(float speed) => Normalize(speed).ToString("0.0", CultureInfo.InvariantCulture);
}
