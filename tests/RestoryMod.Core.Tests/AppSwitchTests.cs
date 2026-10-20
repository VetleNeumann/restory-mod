using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class AppSwitchTests
{
    [Fact]
    public void Right_opens_the_next_app_on_the_taskbar()
    {
        Assert.Equal(2, AppSwitch.Target(count: 4, current: 1, step: 1));
    }

    [Fact]
    public void Right_on_the_last_app_wraps_to_the_first()
    {
        Assert.Equal(0, AppSwitch.Target(count: 4, current: 3, step: 1));
    }

    [Fact]
    public void Left_on_the_first_app_wraps_to_the_last()
    {
        Assert.Equal(3, AppSwitch.Target(count: 4, current: 0, step: -1));
    }

    [Fact]
    public void Right_from_the_desktop_opens_the_first_app()
    {
        Assert.Equal(0, AppSwitch.Target(count: 4, current: -1, step: 1));
    }

    [Fact]
    public void Left_from_the_desktop_opens_the_last_app()
    {
        Assert.Equal(3, AppSwitch.Target(count: 4, current: -1, step: -1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void An_empty_taskbar_has_nothing_to_open(int step)
    {
        Assert.Equal(-1, AppSwitch.Target(count: 0, current: -1, step));
    }
}
