using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class CursorSpeedTests
{
    [Theory]
    [InlineData(0f, CursorSpeed.Min)]
    [InlineData(0.5f, 1f)]
    [InlineData(1f, CursorSpeed.Max)]
    [InlineData(-1f, CursorSpeed.Min)]
    [InlineData(2f, CursorSpeed.Max)]
    public void FromSlider_maps_ends_and_middle(float t, float expected)
    {
        Assert.Equal(expected, CursorSpeed.FromSlider(t), precision: 4);
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(1.7f)]
    [InlineData(4f)]
    public void ToSlider_inverts_FromSlider(float speed)
    {
        Assert.Equal(speed, CursorSpeed.FromSlider(CursorSpeed.ToSlider(speed)), precision: 4);
    }

    [Fact]
    public void Halving_and_doubling_are_equidistant_from_default()
    {
        var mid = CursorSpeed.ToSlider(1f);
        Assert.Equal(mid - CursorSpeed.ToSlider(0.5f), CursorSpeed.ToSlider(2f) - mid, precision: 4);
    }

    [Theory]
    [InlineData(0f, CursorSpeed.Min)]
    [InlineData(100f, CursorSpeed.Max)]
    [InlineData(float.NaN, CursorSpeed.Default)]
    public void Clamp_handles_out_of_range(float input, float expected)
    {
        Assert.Equal(expected, CursorSpeed.Clamp(input));
    }
}
