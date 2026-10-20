namespace RestoryMod.Core;

/// <summary>
/// Which PC app an App switch opens, by index along the taskbar in on-screen order (left first);
/// -1 for none. See CONTEXT.md: App switch.
/// </summary>
public static class AppSwitch
{
    /// <summary>
    /// The PC app a D-pad press opens, right (+1) or left (-1), wrapping at the ends. With none open
    /// (-1), right opens the first and left the last.
    /// </summary>
    public static int Target(int count, int current, int step)
    {
        if (count == 0) return -1;
        if (current < 0) return step < 0 ? count - 1 : 0;
        return ((current + System.Math.Sign(step)) % count + count) % count;
    }
}
