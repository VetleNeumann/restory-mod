using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.Core;

/// <summary>
/// Where an L1/R1 or L2/R2 press moves to along a row of entries: a Category bar, the Browser tabs
/// or the Tool window's slots.
/// </summary>
public static class ShoulderStep
{
    /// <summary>
    /// The next selectable entry from <paramref name="current"/> in the direction of
    /// <paramref name="step"/> (+1 next, -1 previous), wrapping at the ends. With no current entry
    /// (-1), the first selectable entry counts as current. -1 when none is selectable.
    /// </summary>
    public static int Cycle(IReadOnlyList<bool> selectable, int current, int step)
    {
        var count = selectable.Count;
        var direction = step < 0 ? -1 : 1;
        var index = current >= 0 ? current : FirstSelectable(selectable);
        if (index < 0) return -1;
        for (var tried = 0; tried < count; tried++)
        {
            index = ((index + direction) % count + count) % count;
            if (selectable[index]) return index;
        }
        return -1;
    }

    private static int FirstSelectable(IReadOnlyList<bool> selectable)
    {
        for (var i = 0; i < selectable.Count; i++)
        {
            if (selectable[i]) return i;
        }
        return -1;
    }

    /// <summary>
    /// Entry indices in on-screen order, from screen positions (y grows upwards): left to right for a
    /// row, top to bottom for a column. The game's own lists don't always run that way.
    /// </summary>
    public static int[] ScreenOrder(IReadOnlyList<(float X, float Y)> positions)
    {
        var indices = Enumerable.Range(0, positions.Count).ToArray();
        if (positions.Count < 2) return indices;
        var xSpan = positions.Max(p => p.X) - positions.Min(p => p.X);
        var ySpan = positions.Max(p => p.Y) - positions.Min(p => p.Y);
        return xSpan >= ySpan
            ? indices.OrderBy(i => positions[i].X).ToArray()
            : indices.OrderByDescending(i => positions[i].Y).ToArray();
    }

    /// <summary>The Look direction after an L2 (-1) or R2 (+1) press: one step, stopping at the sides.</summary>
    public static LookDirection Look(LookDirection current, int step) => (LookDirection)System.Math.Max(
        (int)LookDirection.Left, System.Math.Min((int)LookDirection.Right, (int)current + System.Math.Sign(step)));
}

/// <summary>Which way the player faces away from the workbench, in left-to-right order.</summary>
public enum LookDirection
{
    Left,
    Main,
    Right,
}
