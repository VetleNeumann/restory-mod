using System;

namespace RestoryMod.Core;

/// <summary>
/// The fixed ways the hint marks a thing (glossary: Look). A waiting part's ghost is the same look
/// shown faint; screws keep the game's own pulse look.
/// </summary>
public enum Look
{
    /// <summary>A ready part: the pulse's own color on the ghost's outline and fill.</summary>
    Ready,

    /// <summary>A missing socket: the pulse's brightness in red.</summary>
    Missing,
}

/// <summary>A color, 0..1 per channel, kept apart from the engine's so the looks can be tested.</summary>
public readonly record struct Rgba(float R, float G, float B, float A);

/// <summary>
/// How one half of a look is drawn: in plain view, or in the hidden style where something stands in
/// front. <see cref="Width"/> scales the pulse's own outline width.
/// </summary>
public readonly record struct LookPaint(Rgba Outline, Rgba Fill, float Width);

public static class Looks
{
    // Waiting: the pulse's peak color held still and darkened (the outline ignores alpha).
    private static readonly Rgba Faint = new(0.45f, 0.45f, 0f, 1f);
    private const float FaintFill = 0.08f;

    // The pulse runs white (blue 1) to yellow (blue 0): the fill thickens as it brightens.
    private const float DimFill = 0.15f;
    private const float BrightFill = 0.35f;

    // The hidden style: half as wide, half as bright, and no fill, so only the outline shows through.
    private const float HiddenWidth = 0.5f;
    private const float HiddenBrightness = 0.5f;

    /// <summary>The look a hint item wears, or null where the game's own pulse look stays.</summary>
    public static Look? OfHint(HintKind kind) => kind switch
    {
        HintKind.Screw or HintKind.ScrewSlot => null,
        HintKind.Missing => Look.Missing,
        _ => Look.Ready,
    };

    /// <summary>A look at the pulse's current color; faint for a waiting part, hidden behind something.</summary>
    public static LookPaint Paint(Look look, bool faint, Rgba pulse, bool hidden)
    {
        var tone = faint ? Faint with { A = pulse.A } : pulse;
        var color = look == Look.Missing ? Red(tone) : tone;
        var alpha = faint ? FaintFill : Lerp(DimFill, BrightFill, 1f - tone.B);
        var paint = new LookPaint(color, color with { A = alpha }, 1f);
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
