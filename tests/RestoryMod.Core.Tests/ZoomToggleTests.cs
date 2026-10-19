using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class ZoomToggleTests
{
    [Theory]
    [InlineData(0f, 1)]
    [InlineData(0.49f, 1)]
    [InlineData(0.5f, -1)]
    [InlineData(1f, -1)]
    public void Toggle_zooms_in_when_out_and_out_when_in(float zoom, int expected)
    {
        Assert.Equal(expected, ZoomToggle.Direction(zoom));
    }

    [Theory]
    [InlineData(1, 1f)]
    [InlineData(-1, 0f)]
    public void Toggle_goes_all_the_way(int direction, float expected)
    {
        Assert.Equal(expected, ZoomToggle.End(direction));
    }
}
