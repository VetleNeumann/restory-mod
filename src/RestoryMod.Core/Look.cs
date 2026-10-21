using System;

namespace RestoryMod.Core;

/// <summary>
/// The fixed ways the hint marks a thing (glossary: Look).
/// </summary>
public enum Look
{
    /// <summary>A ready part: the pulse's own color on the ghost's outline and fill.</summary>
    Ready,

    /// <summary>A missing socket: the pulse's brightness in red.</summary>
    Missing,

    /// <summary>A screw or screw slot: the game's own pulse look, given the hidden style behind things.</summary>
    Screw,

    /// <summary>A ready part known to be burnt: the pulse in the game's burnt colour.</summary>
    Burnt,

    /// <summary>A ready part known to be damaged: the pulse in the game's damaged colour.</summary>
    Damaged,

    /// <summary>A ready part known to be dirty: the pulse in the game's dirty colour.</summary>
    Dirty,
}

/// <summary>A color, 0..1 per channel, kept apart from the engine's so the looks can be tested.</summary>
public readonly record struct Rgba(float R, float G, float B, float A);

/// <summary>How a look's fill covers the ghost.</summary>
public enum FillPattern
{
    /// <summary>Evenly, see-through.</summary>
    Solid,

    /// <summary>A slowly drifting grid of dots with empty gaps between them: half there, so a gap.</summary>
    Dots,

    /// <summary>The pulse's own fill, as the game draws it; <see cref="LookPaint.Fill"/> is unused.</summary>
    Pulse,
}

/// <summary>
/// How one half of a look is drawn: in plain view, or in the hidden style where something stands in
/// front. <see cref="Width"/> scales the pulse's own outline width.
/// </summary>
public readonly record struct LookPaint(Rgba Outline, Rgba Fill, float Width, FillPattern Pattern = FillPattern.Solid);

public static class Looks
{
    // The pulse runs white (blue 1) to yellow (blue 0): the fill thickens as it brightens. Kept light
    // (0.15 to 0.35 hid too much of the part in play).
    private const float DimFill = 0.10f;
    private const float BrightFill = 0.22f;

    // Dots cover a fraction of the ghost, so each dot is drawn this much stronger than a solid fill.
    private const float DotStrength = 3.5f;

    // The hidden style: half as wide, a third as bright, and no fill, so only a faint outline shows
    // through (0.5 bright was too strong in play).
    private const float HiddenWidth = 0.5f;
    private const float HiddenBrightness = 0.3f;

    // A condition look at the bottom of the pulse, as a share of its full colour at the peak.
    private const float DimCondition = 0.55f;

    // Stand-ins for the game's condition outline colours, should its settings not be found.
    private static readonly Rgba BurntColor = new(0.55f, 0.2f, 0.05f, 1f);
    private static readonly Rgba DamagedColor = new(0.95f, 0.15f, 0.1f, 1f);
    private static readonly Rgba DirtyColor = new(0.65f, 0.45f, 0.15f, 1f);

    /// <summary>The look a hint item wears; a part known to need work wears its condition.</summary>
    public static Look OfHint(HintKind kind, PartCondition? condition = null) => kind switch
    {
        HintKind.Screw or HintKind.ScrewSlot => Look.Screw,
        HintKind.Missing => Look.Missing,
        HintKind.Part or HintKind.LoosePart when condition == PartCondition.Burnt => Look.Burnt,
        HintKind.Part or HintKind.LoosePart when condition == PartCondition.Damaged => Look.Damaged,
        HintKind.Part or HintKind.LoosePart when condition == PartCondition.Dirty => Look.Dirty,
        _ => Look.Ready,
    };

    public static bool IsCondition(this Look look) => look.Condition() is not null;

    /// <summary>The condition a condition look shows; null for any other look.</summary>
    public static PartCondition? Condition(this Look look) => look switch
    {
        Look.Burnt => PartCondition.Burnt,
        Look.Damaged => PartCondition.Damaged,
        Look.Dirty => PartCondition.Dirty,
        _ => null,
    };

    /// <summary>
    /// A look at the pulse's current color, in plain view or hidden behind something. A condition look
    /// takes <paramref name="condition"/>, the game's outline colour for it, brightening with the pulse.
    /// </summary>
    public static LookPaint Paint(Look look, Rgba pulse, bool hidden, Rgba? condition = null)
    {
        var missing = look == Look.Missing;
        var color = missing ? Red(pulse) : pulse;
        if (look.IsCondition())
        {
            var full = condition ?? look switch
            {
                Look.Burnt => BurntColor,
                Look.Damaged => DamagedColor,
                _ => DirtyColor,
            };
            color = Scale(full, Lerp(DimCondition, 1f, 1f - pulse.B)) with { A = 1f };
        }
        var alpha = Lerp(DimFill, BrightFill, 1f - pulse.B);
        if (missing) alpha *= DotStrength;
        var pattern = look switch
        {
            Look.Missing => FillPattern.Dots,
            Look.Screw => FillPattern.Pulse,
            _ => FillPattern.Solid,
        };
        var paint = new LookPaint(color, color with { A = alpha }, 1f, pattern);
        if (!hidden) return paint;
        return new LookPaint(
            Scale(paint.Outline, HiddenBrightness),
            paint.Fill with { A = 0f },
            HiddenWidth);
    }

    // Keeps the pulse's brightness and turns it red.
    private static Rgba Red(Rgba c) => c with { G = c.G * 0.2f, B = c.B * 0.2f };

    private static Rgba Scale(Rgba c, float k) => new(c.R * k, c.G * k, c.B * k, c.A);

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}
