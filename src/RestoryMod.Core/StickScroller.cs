using System;

namespace RestoryMod.Core;

/// <summary>Tuning for <see cref="StickScroller"/>.</summary>
public sealed record StickScrollSettings(
    float InnerDeadzone,
    float OuterDeadzone,
    float Exponent,
    float Speed,
    float Smoothing,
    bool Invert,
    float MaxSpeed,
    float RampTime)
{
    public static StickScrollSettings Default { get; } = new(0.2f, 0.925f, 2f, 1.5f, 0.07f, false, 3f, 1f);
}

/// <summary>Stick scroll: glides a vertical scroll view at a speed that follows the stick's tilt.</summary>
public sealed class StickScroller
{
    /// <summary>Current speed in view heights per second; positive scrolls toward the top.</summary>
    private float velocity;

    /// <summary>Seconds the stick has been held near full tilt; drives the ramp to max speed.</summary>
    private float heldFull;
    private int heldSign;

    /// <summary>Rescaled tilt beyond which holding ramps toward max speed.</summary>
    private const float FullTilt = 0.9f;

    /// <summary>Below this speed (view heights per second) with the stick centred, the glide is over.</summary>
    private const float RestSpeed = 0.01f;

    public bool IsMoving => velocity != 0f;

    /// <summary>True when this stick position would scroll: past the deadzone and mostly vertical.</summary>
    public static bool IsPushed(float stickX, float stickY, StickScrollSettings settings) =>
        Tilt(stickX, stickY, settings, out _) != 0f;

    public float Advance(float position, float stickX, float stickY, float deltaTime,
        float viewportHeight, float contentHeight, StickScrollSettings settings)
    {
        var range = contentHeight - viewportHeight;
        if (range <= 0f)
        {
            velocity = 0f;
            return position;
        }

        var tilt = Tilt(stickX, stickY, settings, out var scaled);
        // A flick from full up to full down keeps the tilt high; it is a new push, not a held one.
        if (scaled < FullTilt || Math.Sign(tilt) != heldSign) heldFull = 0f;
        heldSign = Math.Sign(tilt);
        var ramp = settings.RampTime > 0f ? Math.Min(1f, heldFull / settings.RampTime) : 1f;
        if (scaled >= FullTilt) heldFull += deltaTime;
        var speed = settings.Speed + Math.Max(0f, settings.MaxSpeed - settings.Speed) * ramp;
        var target = tilt * speed * (settings.Invert ? -1f : 1f);
        // Speed approaches the target exponentially; the distance is that curve's exact integral over
        // the frame, so the same push covers the same distance at any frame rate.
        float distance;
        if (settings.Smoothing > 0f)
        {
            var approach = 1f - (float)Math.Exp(-deltaTime / settings.Smoothing);
            distance = target * deltaTime + (velocity - target) * settings.Smoothing * approach;
            velocity += (target - velocity) * approach;
        }
        else
        {
            velocity = target;
            distance = target * deltaTime;
        }
        if (target == 0f && Math.Abs(velocity) < RestSpeed) velocity = 0f;

        var pixels = distance * viewportHeight;
        var next = position + pixels / range;
        if (next is >= 0f and <= 1f) return next;

        // Hard stop: no bounce, and no stored speed pushing into the end.
        velocity = 0f;
        heldFull = 0f;
        return next < 0f ? 0f : 1f;
    }

    /// <summary>
    /// Vertical tilt in [-1, 1] after a scaled radial deadzone and the response curve: the stick's
    /// distance from centre is rescaled so the inner deadzone maps to 0 and the outer to 1.
    /// </summary>
    private static float Tilt(float x, float y, StickScrollSettings settings, out float scaled)
    {
        scaled = 0f;
        var magnitude = (float)Math.Sqrt(x * x + y * y);
        // Mostly sideways is a horizontal intent, not a slow vertical one.
        if (magnitude <= settings.InnerDeadzone || Math.Abs(x) >= Math.Abs(y)) return 0f;

        scaled = (magnitude - settings.InnerDeadzone) / (settings.OuterDeadzone - settings.InnerDeadzone);
        scaled = Math.Min(1f, scaled);
        var curved = (float)Math.Pow(scaled, settings.Exponent);
        return curved * y / magnitude;
    }
}
