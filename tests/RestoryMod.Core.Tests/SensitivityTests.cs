using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class SensitivityTests
{
    [Fact]
    public void Steps_cover_range_in_tenths()
    {
        Assert.Equal(38, Sensitivity.SliderSteps);
        Assert.Equal(Sensitivity.Min, Sensitivity.FromStep(0));
        Assert.Equal(Sensitivity.Max, Sensitivity.FromStep(Sensitivity.SliderSteps));
        Assert.Equal(1f, Sensitivity.FromStep(Sensitivity.ToStep(Sensitivity.Default)));
    }

    [Fact]
    public void Every_step_is_a_clean_tenth_and_round_trips()
    {
        for (var step = 0; step <= Sensitivity.SliderSteps; step++)
        {
            var speed = Sensitivity.FromStep(step);
            Assert.Equal(step, Sensitivity.ToStep(speed));
            Assert.Equal((0.2m + step * 0.1m).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                Sensitivity.Format(speed));
        }
    }

    [Theory]
    [InlineData(1.74f, 1.7f)]
    [InlineData(1.87f, 1.9f)]
    [InlineData(0f, Sensitivity.Min)]
    [InlineData(100f, Sensitivity.Max)]
    [InlineData(float.NaN, Sensitivity.Default)]
    public void Normalize_clamps_and_snaps_to_tenths(float input, float expected)
    {
        Assert.Equal(expected, Sensitivity.Normalize(input));
    }

    [Theory]
    [InlineData(1f, "1.0")]
    [InlineData(2.3f, "2.3")]
    [InlineData(4f, "4.0")]
    public void Format_uses_one_decimal_without_suffix(float speed, string expected)
    {
        Assert.Equal(expected, Sensitivity.Format(speed));
    }
}
