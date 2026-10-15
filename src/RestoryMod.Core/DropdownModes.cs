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

    /// <summary>A click at the virtual cursor must be swallowed.</summary>
    public bool SuppressClick => IsOpen && Mode == DropdownMode.Nav;

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
    }

    public void DpadPressed(int? rowUnderCursor)
    {
        Mode = DropdownMode.Nav;
        cursorHidden = true;
        navStartRow = rowUnderCursor;
    }

    public void CursorMoved(int? rowUnderCursor)
    {
        cursorHidden = false;
        if (Mode == DropdownMode.Nav && rowUnderCursor is not null && rowUnderCursor != navStartRow)
            Mode = DropdownMode.Pointer;
    }

    public void Close() => IsOpen = false;
}
