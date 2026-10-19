namespace RestoryMod.Core;

/// <summary>The workbench zoom toggle: zoom runs from 0 (out) to 1 (all the way in).</summary>
public static class ZoomToggle
{
    /// <summary>+1 zooms in from the zoomed-out half, -1 zooms out from the other.</summary>
    public static int Direction(float zoom) => zoom < 0.5f ? 1 : -1;

    /// <summary>Where a toggle in <paramref name="direction"/> ends: all the way in or all the way out.</summary>
    public static float End(int direction) => direction > 0 ? 1f : 0f;
}
