namespace RestoryMod.Core;

/// <summary>How a gamepad drives an open dropdown. See CONTEXT.md: Pointer mode, Nav mode.</summary>
public enum DropdownMode
{
    Pointer,
    Nav,
}

/// <summary>
/// Decides, while a dropdown is open, whether the gamepad acts through the virtual cursor
/// (pointer mode) or through the highlighted row (nav mode). Rows are opaque ids; null means
/// the cursor is over no row.
/// </summary>
public sealed class DropdownModes
{
    public bool IsOpen { get; private set; }
    public DropdownMode Mode { get; private set; }

    /// <summary>
    /// A click at the virtual cursor must be swallowed. Once A goes down in nav mode this holds
    /// until A is released, even if its pick closes the list, so the press never also clicks.
    /// </summary>
    public bool SuppressClick => (IsOpen && Mode == DropdownMode.Nav) || clickHeldBack;

    private bool clickHeldBack;

    /// <summary>
    /// A closed dropdown that still holds the selection must ignore A's submit, because the
    /// latest input was the cursor and A is a click at the cursor.
    /// </summary>
    public bool SuppressHeaderSubmit => !IsOpen && cursorLastUsed;

    private bool cursorLastUsed;

    /// <summary>Only the D-pad moves the highlighted row; a stick tilt is the cursor's.</summary>
    public bool SuppressStickMove => IsOpen && !dpadHeld;

    private bool dpadHeld;

    /// <summary>A submit to the highlighted row must be swallowed.</summary>
    public bool SuppressSubmit => IsOpen && Mode == DropdownMode.Pointer;

    /// <summary>The virtual cursor must not be drawn.</summary>
    public bool CursorHidden => IsOpen && cursorHidden;

    private bool cursorHidden;

    /// <summary>The row the cursor was over when nav mode began; leaving it ends nav mode.</summary>
    private int? navStartRow;

    public void Open(DropdownMode initial, int? rowUnderCursor)
    {
        IsOpen = true;
        Mode = initial;
        cursorHidden = initial == DropdownMode.Nav;
        navStartRow = rowUnderCursor;
        cursorLastUsed = initial == DropdownMode.Pointer;
    }

    /// <summary>Reports this frame's D-pad state; the press edge counts as <see cref="DpadPressed"/>.</summary>
    public void DpadHeld(bool held, int? rowUnderCursor)
    {
        if (held && !dpadHeld) DpadPressed(rowUnderCursor);
        dpadHeld = held;
    }

    public void DpadPressed(int? rowUnderCursor)
    {
        Mode = DropdownMode.Nav;
        cursorHidden = true;
        navStartRow = rowUnderCursor;
        cursorLastUsed = false;
    }

    public void CursorMoved(int? rowUnderCursor)
    {
        cursorHidden = false;
        if (Mode == DropdownMode.Nav && rowUnderCursor is not null && rowUnderCursor != navStartRow)
            Mode = DropdownMode.Pointer;
        cursorLastUsed = !IsOpen || Mode == DropdownMode.Pointer;
    }

    public void SubmitPressed()
    {
        if (SuppressClick) clickHeldBack = true;
    }

    public void SubmitReleased() => clickHeldBack = false;

    public void Close() => IsOpen = false;
}
