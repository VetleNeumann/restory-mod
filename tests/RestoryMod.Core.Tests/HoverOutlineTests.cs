using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class HoverOutlineTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void The_hover_highlight_never_shows_on_a_blocked_part(bool blocked, bool switchable, bool shows)
    {
        Assert.Equal(shows, HoverOutline.Shows(blocked, switchable));
    }
}
