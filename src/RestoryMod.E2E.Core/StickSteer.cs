using System;

namespace RestoryMod.E2E;

/// <summary>A screen point or stick tilt; screen points in pixels with y up, tilts from -1 to 1.</summary>
public readonly record struct Vec2(float X, float Y);

/// <summary>
/// Closed-loop aiming of the virtual cursor with the left stick: each frame, tilt the stick towards the
/// target, easing off within <c>slowWithin</c> pixels so the cursor doesn't overshoot.
/// </summary>
public sealed class StickSteer
{
    private readonly float tolerance;
    private readonly float slowWithin;
    private readonly float minTilt;

    public StickSteer(float tolerance, float slowWithin, float minTilt)
    {
        this.tolerance = tolerance;
        this.slowWithin = slowWithin;
        this.minTilt = minTilt;
    }

    /// <summary>The cursor is within the tolerance of the target.</summary>
    public bool Arrived(Vec2 cursor, Vec2 target) => Distance(cursor, target) <= tolerance;

    /// <summary>
    /// The stick tilt that moves the cursor from <paramref name="cursor"/> towards <paramref name="target"/>;
    /// at rest once it has arrived.
    /// </summary>
    public Vec2 Tilt(Vec2 cursor, Vec2 target)
    {
        var distance = Distance(cursor, target);
        if (distance <= tolerance) return default;
        var dx = target.X - cursor.X;
        var dy = target.Y - cursor.Y;
        var tilt = Math.Max(minTilt, Math.Min(1f, distance / slowWithin));
        return new Vec2(dx / distance * tilt, dy / distance * tilt);
    }

    private static float Distance(Vec2 a, Vec2 b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
