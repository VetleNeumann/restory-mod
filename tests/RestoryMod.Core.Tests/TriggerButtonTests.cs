using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class TriggerButtonTests
{
    [Theory]
    [InlineData(0.6f, 0.4f, true)]
    [InlineData(1f, 0f, true)]
    [InlineData(0.6f, 0.55f, false)]
    [InlineData(0.4f, 0f, false)]
    [InlineData(0f, 1f, false)]
    public void Pressed_when_pulled_past_halfway_this_frame(float value, float previous, bool expected)
    {
        Assert.Equal(expected, TriggerButton.Pressed(value, previous));
    }

    [Theory]
    [InlineData(0.3f, true)]
    [InlineData(0.2f, false)]
    [InlineData(0f, false)]
    public void Stays_down_until_nearly_let_go(float value, bool expected)
    {
        Assert.Equal(expected, TriggerButton.Down(value));
    }

    [Theory]
    [InlineData(0f, 1)]
    [InlineData(0.49f, 1)]
    [InlineData(0.5f, -1)]
    [InlineData(1f, -1)]
    public void Toggle_zooms_in_when_out_and_out_when_in(float zoom, int expected)
    {
        Assert.Equal(expected, TriggerButton.ToggleDirection(zoom));
    }
}
