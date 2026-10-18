using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class PulseRewindTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    public void Rising_pulse_keeps_its_place(float elapsed)
    {
        Assert.Equal(elapsed, PulseRewind.Position(elapsed, toPeak: 0.5f, toBase: 0.5f), 4);
    }

    [Theory]
    [InlineData(0.6f, 0.4f)]
    [InlineData(0.9f, 0.1f)]
    [InlineData(1f, 0f)]
    public void Fading_pulse_turns_back_at_the_same_brightness(float elapsed, float expected)
    {
        Assert.Equal(expected, PulseRewind.Position(elapsed, toPeak: 0.5f, toBase: 0.5f), 4);
    }

    [Fact]
    public void Uneven_halves_mirror_by_fraction_of_each_half()
    {
        // A quarter of the way down a 1 s fade is a quarter short of the peak on a 0.5 s rise.
        Assert.Equal(0.375f, PulseRewind.Position(0.75f, toPeak: 0.5f, toBase: 1f), 4);
    }

    [Theory]
    [InlineData(5f)]
    [InlineData(-1f)]
    public void Out_of_range_clamps_to_the_pulse(float elapsed)
    {
        var position = PulseRewind.Position(elapsed, toPeak: 0.5f, toBase: 0.5f);
        Assert.InRange(position, 0f, 0.5f);
    }

    [Fact]
    public void Zero_length_fade_restarts_from_the_peak()
    {
        Assert.Equal(0.5f, PulseRewind.Position(0.5001f, toPeak: 0.5f, toBase: 0f), 4);
    }
}
