using System;
using System.Collections.Generic;

namespace RestoryMod.E2E;

/// <summary>
/// Where to steer the cursor to scrub over a part: a zigzag across its screen bounds, row by row from the
/// top, turning at each end, so a held tool passes over all of it.
/// </summary>
public static class SweepPath
{
    /// <summary>
    /// The turning points of the zigzag over the box from <paramref name="min"/> to <paramref name="max"/>,
    /// its rows evenly spaced and no further apart than <paramref name="rowSpacing"/>.
    /// </summary>
    public static IReadOnlyList<Vec2> Zigzag(Vec2 min, Vec2 max, float rowSpacing)
    {
        var height = max.Y - min.Y;
        var gaps = (int)Math.Ceiling(height / rowSpacing);
        var points = new List<Vec2>();
        for (var row = 0; row <= gaps; row++)
        {
            var y = gaps == 0 ? max.Y : max.Y - height * row / gaps;
            var (from, to) = row % 2 == 0 ? (min.X, max.X) : (max.X, min.X);
            points.Add(new Vec2(from, y));
            points.Add(new Vec2(to, y));
        }
        return points;
    }
}
