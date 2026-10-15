using System;

namespace RestoryMod.Core;

/// <summary>
/// Gamepad cursor speed multiplier. The game hardcodes Rewired's PlayerMouse.pointerSpeed to 1
/// for gamepads; the mod replaces it with a value in [<see cref="Min"/>, <see cref="Max"/>].
/// The slider maps logarithmically so 1x sits in the middle and halving/doubling feel symmetric.
/// </summary>
public static class CursorSpeed
{
    public const float Min = 0.25f;
    public const float Max = 4f;
    public const float Default = 1f;

    public static float Clamp(float speed)
    {
        if (float.IsNaN(speed)) return Default;
        return Math.Min(Max, Math.Max(Min, speed));
    }

    /// <summary>Slider position in [0, 1] to speed multiplier. 0 → Min, 0.5 → 1x, 1 → Max.</summary>
    public static float FromSlider(float t)
    {
        t = Math.Min(1f, Math.Max(0f, t));
        return Min * (float)Math.Pow(Max / Min, t);
    }

    /// <summary>Speed multiplier to slider position in [0, 1]. Inverse of <see cref="FromSlider"/>.</summary>
    public static float ToSlider(float speed)
    {
        speed = Clamp(speed);
        return (float)(Math.Log(speed / Min) / Math.Log(Max / Min));
    }

    /// <summary>Speed rounded to two decimals for display and storage.</summary>
    public static float Round(float speed) => (float)Math.Round(Clamp(speed), 2);
}
