namespace RestoryMod.Core;

/// <summary>
/// Rules for the Notepad key at the workbench. The game ignores its Notepad key there, leaving the
/// sliding Notepad mouse-only; the mod makes the key switch it between Peeking and Open.
/// "Key open" is the mod's sticky Open, kept until the key is pressed again, the Notepad is
/// pinned, or the workbench session ends. Sliding mode is the game's term for the workbench panel.
/// </summary>
public static class NotepadToggle
{
    /// <summary>Key open after a Notepad key press. Ignored while hidden, pinned or not sliding.</summary>
    public static bool AfterKeyPress(bool visible, bool slidingMode, bool pinned, bool keyOpen) =>
        visible && slidingMode && !pinned ? !keyOpen : keyOpen;

    /// <summary>
    /// Key open after any other Notepad state change. Pinning takes over (so unpinning returns to
    /// Peeking), and leaving sliding mode ends the workbench session.
    /// </summary>
    public static bool KeepKeyOpen(bool slidingMode, bool pinned, bool keyOpen) =>
        keyOpen && slidingMode && !pinned;
}
