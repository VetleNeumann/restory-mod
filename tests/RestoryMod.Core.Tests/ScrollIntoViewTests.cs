using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class ScrollIntoViewTests
{
    // Viewport spans y = -100 (bottom) to 0 (top); y grows upward, as in Unity UI.

    [Fact]
    public void Visible_row_needs_no_scroll()
    {
        Assert.Equal(0f, ScrollIntoView.ContentShift(rowTop: -20, rowBottom: -40, viewTop: 0, viewBottom: -100));
    }

    [Fact]
    public void Row_below_the_view_lifts_the_content_until_its_bottom_shows()
    {
        Assert.Equal(30f, ScrollIntoView.ContentShift(rowTop: -110, rowBottom: -130, viewTop: 0, viewBottom: -100));
    }

    [Fact]
    public void Row_above_the_view_lowers_the_content_until_its_top_shows()
    {
        Assert.Equal(-25f, ScrollIntoView.ContentShift(rowTop: 25, rowBottom: 5, viewTop: 0, viewBottom: -100));
    }

    [Fact]
    public void Row_taller_than_the_view_aligns_its_top()
    {
        Assert.Equal(-10f, ScrollIntoView.ContentShift(rowTop: 10, rowBottom: -150, viewTop: 0, viewBottom: -100));
    }
}
