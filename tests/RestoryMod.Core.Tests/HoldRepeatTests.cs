using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class HoldRepeatTests
{
    [Fact]
    public void A_press_steps_at_once()
    {
        var repeat = new HoldRepeat(delay: 0.3f, interval: 0.1f);
        Assert.Equal(1, repeat.Update(direction: 1, deltaTime: 0.016f));
    }

    [Fact]
    public void Holding_waits_out_the_delay_then_steps_every_interval()
    {
        var repeat = new HoldRepeat(delay: 0.3f, interval: 0.1f);
        repeat.Update(direction: -1, deltaTime: 0f);

        Assert.Equal(0, repeat.Update(direction: -1, deltaTime: 0.2f));
        Assert.Equal(-1, repeat.Update(direction: -1, deltaTime: 0.15f));
        // 0.05 s overshot the delay, so the next step comes 0.05 s later, not a whole interval.
        Assert.Equal(0, repeat.Update(direction: -1, deltaTime: 0.04f));
        Assert.Equal(-1, repeat.Update(direction: -1, deltaTime: 0.02f));
    }

    [Fact]
    public void Nothing_held_takes_no_step()
    {
        var repeat = new HoldRepeat(delay: 0.3f, interval: 0.1f);
        repeat.Update(direction: 1, deltaTime: 0f);
        Assert.Equal(0, repeat.Update(direction: 0, deltaTime: 1f));
    }

    [Fact]
    public void Pressing_again_after_release_steps_at_once()
    {
        var repeat = new HoldRepeat(delay: 0.3f, interval: 0.1f);
        repeat.Update(direction: 1, deltaTime: 0f);
        repeat.Update(direction: 0, deltaTime: 0.05f);
        Assert.Equal(1, repeat.Update(direction: 1, deltaTime: 0.05f));
    }

    [Fact]
    public void Switching_direction_steps_at_once_the_new_way()
    {
        var repeat = new HoldRepeat(delay: 0.3f, interval: 0.1f);
        repeat.Update(direction: 1, deltaTime: 0f);
        Assert.Equal(-1, repeat.Update(direction: -1, deltaTime: 0.05f));
    }
}
