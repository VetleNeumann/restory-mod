namespace RestoryMod.Core;

/// <summary>Scrolling a list so a row is fully visible. Coordinates grow upward, as in Unity UI.</summary>
public static class ScrollIntoView
{
    /// <summary>
    /// How far to move the scrolled content up (positive) or down (negative) so the row shows,
    /// all four edges in the viewport's space. A row taller than the viewport is aligned by its top.
    /// </summary>
    public static float ContentShift(float rowTop, float rowBottom, float viewTop, float viewBottom)
    {
        if (rowTop > viewTop) return viewTop - rowTop;
        if (rowBottom < viewBottom) return viewBottom - rowBottom;
        return 0f;
    }
}
