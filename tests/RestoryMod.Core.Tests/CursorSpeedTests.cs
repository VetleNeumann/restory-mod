using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class CursorSpeedTests
{
    [Fact]
    public void Steps_cover_range_in_tenths()
    {
        Assert.Equal(38, CursorSpeed.SliderSteps);
        Assert.Equal(CursorSpeed.Min, CursorSpeed.FromStep(0));
        Assert.Equal(CursorSpeed.Max, CursorSpeed.FromStep(CursorSpeed.SliderSteps));
        Assert.Equal(1f, CursorSpeed.FromStep(CursorSpeed.ToStep(CursorSpeed.Default)));
    }

    [Fact]
    public void Every_step_is_a_clean_tenth_and_round_trips()
    {
        for (var step = 0; step <= CursorSpeed.SliderSteps; step++)
        {
            var speed = CursorSpeed.FromStep(step);
            Assert.Equal(step, CursorSpeed.ToStep(speed));
            Assert.Equal((0.2m + step * 0.1m).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                CursorSpeed.Format(speed));
        }
    }

    [Theory]
    [InlineData(1.74f, 1.7f)]
    [InlineData(1.87f, 1.9f)]
    [InlineData(0f, CursorSpeed.Min)]
    [InlineData(100f, CursorSpeed.Max)]
    [InlineData(float.NaN, CursorSpeed.Default)]
    public void Normalize_clamps_and_snaps_to_tenths(float input, float expected)
    {
        Assert.Equal(expected, CursorSpeed.Normalize(input));
    }

    [Theory]
    [InlineData(1f, "1.0")]
    [InlineData(2.3f, "2.3")]
    [InlineData(4f, "4.0")]
    public void Format_uses_one_decimal_without_suffix(float speed, string expected)
    {
        Assert.Equal(expected, CursorSpeed.Format(speed));
    }
}
