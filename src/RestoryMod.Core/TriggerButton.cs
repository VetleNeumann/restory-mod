namespace RestoryMod.Core;

/// <summary>
/// A gamepad trigger (an axis from 0 to 1) used as a button: pressed when it is pulled past halfway,
/// and counted as held down above a quarter, so a press held loosely around halfway stays held.
/// </summary>
public static class TriggerButton
{
    public const float PressAt = 0.5f;
    public const float ReleaseBelow = 0.25f;

    public static bool Pressed(float value, float previous) => value >= PressAt && previous < PressAt;

    public static bool Down(float value) => value >= ReleaseBelow;
}
