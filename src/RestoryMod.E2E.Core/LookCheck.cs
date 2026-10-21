using System;
using RestoryMod.Core;

namespace RestoryMod.E2E;

/// <summary>
/// The front half of a hint ghost's outline as drawn: its colour, its width as a share of the pulse's own,
/// and how its fill covers it.
/// </summary>
public readonly record struct DrawnHalf(Rgba Outline, float Width, FillPattern Pattern);

/// <summary>A look a ghost is drawn in, and whether in the hidden style.</summary>
public readonly record struct DrawnLook(Look Look, bool Hidden);

/// <summary>
/// Which look a ghost is drawn in, judged against the look table (<see cref="Looks.Paint"/>), the hint's
/// own rule for how each look is drawn, rather than against the mod's bookkeeping of what it painted: a
/// ghost painted wrong then fails its check.
/// </summary>
public static class LookCheck
{
    // How far a drawn outline may sit from a look's (colour plus width): the pulse moves on between the
    // frame the mod paints and the one the harness reads.
    private const float MaxDistance = 0.35f;

    private static readonly Look[] AllLooks = (Look[])Enum.GetValues(typeof(Look));

    /// <param name="pulse">The pulse's outline colour now.</param>
    /// <param name="conditionColour">The game's outline colour for a condition look, or null for the look's stand-in.</param>
    /// <returns>The nearest look, or null when the outline is no look's.</returns>
    public static DrawnLook? Classify(DrawnHalf front, Rgba pulse, Func<Look, Rgba?> conditionColour)
    {
        DrawnLook? best = null;
        var bestDistance = MaxDistance;
        foreach (var look in AllLooks)
        {
            // Only a screw is ever drawn hidden at the front: it is too small to be half hidden.
            foreach (var hidden in look == Look.Screw ? new[] { false, true } : new[] { false })
            {
                var paint = Looks.Paint(look, pulse, hidden, conditionColour(look));
                if (paint.Pattern != front.Pattern) continue;
                var distance = Distance(paint.Outline, front.Outline) + Math.Abs(paint.Width - front.Width);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = new DrawnLook(look, hidden);
            }
        }
        return best;
    }

    private static float Distance(Rgba a, Rgba b)
    {
        float dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return (float)Math.Sqrt(dr * dr + dg * dg + db * db);
    }
}
