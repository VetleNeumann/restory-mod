using System;

namespace RestoryMod.Core;

/// <summary>
/// The drop hint pulse rises to its peak, then fades back. When more screws join a pulse that is
/// already fading, it turns round and rises again from the same brightness, so the new screws get a
/// full peak instead of the tail end of the fade.
/// </summary>
public static class PulseRewind
{
    /// <summary>
    /// Where to move a pulse <paramref name="elapsed"/> seconds in: unchanged while rising, else the
    /// point on the rise mirroring the fade (the same fraction short of the peak).
    /// </summary>
    public static float Position(float elapsed, float toPeak, float toBase)
    {
        toPeak = Math.Max(0f, toPeak);
        if (elapsed <= toPeak) return Math.Max(0f, elapsed);
        if (toBase <= 0f) return toPeak;
        var faded = Math.Min(1f, (elapsed - toPeak) / toBase);
        return toPeak * (1f - faded);
    }

    /// <summary>
    /// While the hint button is held the pulse waits at its peak: where to put it back once it has
    /// passed the peak, or null while it is still rising.
    /// </summary>
    public static float? HoldAtPeak(float elapsed, float toPeak) => elapsed > toPeak ? toPeak : null;
}
