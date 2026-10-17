namespace RestoryMod.Core;

/// <summary>
/// Turns a held direction into steps the way a held arrow key does: one step on the press, then,
/// after <c>delay</c> seconds, one every <c>interval</c> seconds until released.
/// </summary>
public sealed class HoldRepeat(float delay, float interval)
{
    private int held;
    private float untilNext;

    /// <summary>The step to take this frame: the held direction (-1 or +1), or 0 for none.</summary>
    public int Update(int direction, float deltaTime)
    {
        if (direction != held)
        {
            held = direction;
            untilNext = delay;
            return direction;
        }
        if (direction == 0) return 0;

        untilNext -= deltaTime;
        if (untilNext > 0f) return 0;
        // At most one step a frame; a long frame doesn't build up a burst of steps.
        untilNext = System.Math.Max(untilNext + interval, 0f);
        return direction;
    }
}
