using RestoryMod.E2E;

namespace RestoryMod.Core.Tests;

public class SpinMeterTests
{
    [Fact]
    public void The_spin_is_the_mean_turn_over_the_frames_the_device_turned()
    {
        var meter = new SpinMeter();
        foreach (var degrees in new[] { 0f, 0f, 1f, 2f, 3f, 0f }) meter.Frame(degrees);

        Assert.Equal(2f, meter.DegreesPerFrame, precision: 3);
    }
}
