namespace RestoryMod.Core;

/// <summary>
/// The mod's hold on the sliding Notepad. <see cref="KeyOpen"/> is the sticky Open the key gives.
/// <see cref="HoverIgnored"/> is set when the key closes a Notepad the pointer is holding open: the
/// game opens it while the pointer is inside, so the pointer is ignored until it leaves.
/// </summary>
public readonly record struct NotepadKeyState(bool KeyOpen, bool HoverIgnored);

/// <summary>
/// Rules for the Notepad key at the workbench. The game ignores its Notepad key there, leaving the
/// sliding Notepad mouse-only; the mod makes the key switch it between Peeking and Open.
/// "Key open" is the mod's sticky Open, kept until the key is pressed again, the Notepad is
/// pinned, or the workbench session ends. Sliding mode is the game's term for the workbench panel.
/// </summary>
public static class NotepadToggle
{
    /// <summary>
    /// State after a Notepad key press. The key flips what is shown: an Open Notepad goes to Peeking,
    /// even when the pointer holds it open, and a Peeking one opens. Ignored while hidden, pinned or
    /// not sliding.
    /// </summary>
    public static NotepadKeyState AfterKeyPress(bool visible, bool slidingMode, bool pinned, bool pointerInside,
        NotepadKeyState state)
    {
        if (!visible || !slidingMode || pinned) return state;

        var shownOpen = state.KeyOpen || (pointerInside && !state.HoverIgnored);
        return shownOpen
            ? new NotepadKeyState(KeyOpen: false, HoverIgnored: pointerInside)
            : new NotepadKeyState(KeyOpen: true, HoverIgnored: false);
    }

    /// <summary>
    /// Key open after any other Notepad state change. Pinning takes over (so unpinning returns to
    /// Peeking), and leaving sliding mode ends the workbench session.
    /// </summary>
    public static bool KeepKeyOpen(bool slidingMode, bool pinned, bool keyOpen) =>
        keyOpen && slidingMode && !pinned;

    /// <summary>
    /// Ignored hover after any other Notepad state change. Ends when the pointer leaves, so hovering
    /// opens the Notepad again as usual, and like key open on pinning or leaving sliding mode.
    /// </summary>
    public static bool KeepHoverIgnored(bool slidingMode, bool pinned, bool pointerInside, bool hoverIgnored) =>
        hoverIgnored && slidingMode && !pinned && pointerInside;
}
