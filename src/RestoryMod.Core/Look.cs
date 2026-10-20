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

    /// <summary>The look a hint item wears.</summary>
    public static Look OfHint(HintKind kind) => kind switch
    {
        HintKind.Screw or HintKind.ScrewSlot => Look.Screw,
        HintKind.Missing => Look.Missing,
        _ => Look.Ready,
    };

    /// <summary>A look at the pulse's current color, in plain view or hidden behind something.</summary>
    public static LookPaint Paint(Look look, Rgba pulse, bool hidden)
    {
        var missing = look == Look.Missing;
        var color = missing ? Red(pulse) : pulse;
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
