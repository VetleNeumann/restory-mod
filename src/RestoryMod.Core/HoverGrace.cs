namespace RestoryMod.Core;

/// <summary>What the game found under the virtual cursor this frame.</summary>
public enum HoverKind
{
    /// <summary>Nothing the cursor could act on.</summary>
    Nothing,

    /// <summary>A thing Hover grace covers.</summary>
    Eligible,

    /// <summary>Anything else the cursor could act on.</summary>
    Other,
}

/// <summary>Configurable length of the Hover grace window, in milliseconds. 0 turns it off.</summary>
public static class HoverGraceWindow
{
    public const int DefaultMs = 150;
    public const int MaxMs = 500;
}

/// <summary>
/// Hover grace: a thing stays hovered for a short window after the virtual cursor leaves it, so a
/// press just after overshooting still acts on it ("coyote time" in platformers).
/// </summary>
public sealed class HoverGrace<T> where T : class
{
    private T? last;
    private float lastSeen;
    private int lastFrame;

    /// <summary>
    /// Feeds one frame's detection result. Returns the thing to keep hovered in place of what the
    /// game found, or null to leave the game's result alone. A frame skipped since the last call
    /// means the hover state was left (or is dragging what it hovered), so nothing lingers past it.
    /// </summary>
    public T? Next(int frame, float now, HoverKind kind, T? hovered, float window)
    {
        if (frame != lastFrame + 1) last = null;
        lastFrame = frame;

        if (kind == HoverKind.Eligible)
        {
            last = hovered;
            lastSeen = now;
            return null;
        }
        if (kind == HoverKind.Other || now - lastSeen > window) last = null;
        return last;
    }

    /// <summary>Drops the lingering thing, e.g. once the button is pressed.</summary>
    public void Forget() => last = null;
}
