namespace RestoryMod.Core;

/// <summary>
/// A gamepad trigger (an axis from 0 to 1) used as a button: pressed once it is pulled past halfway,
/// held until it is nearly let go, so a trigger resting around halfway doesn't flicker.
/// </summary>
public static class TriggerButton
{
    public const float PressAt = 0.5f;
    public const float ReleaseBelow = 0.25f;

    public static bool Pressed(float value, float previous) => value >= PressAt && previous < PressAt;

    public static bool Down(float value) => value >= ReleaseBelow;

    /// <summary>Workbench zoom toggle: +1 zooms in from the zoomed-out half, -1 zooms out from the other.</summary>
    public static int ToggleDirection(float zoom) => zoom < 0.5f ? 1 : -1;
}
