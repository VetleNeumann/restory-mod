using RestoryMod.E2E;

namespace RestoryMod.Core.Tests;

public class SweepPathTests
{
    [Fact]
    public void The_sweep_runs_row_by_row_from_the_top_turning_at_each_end()
    {
        var path = SweepPath.Zigzag(new Vec2(0f, 0f), new Vec2(20f, 20f), rowSpacing: 10f);

        Assert.Equal(new[]
        {
            new Vec2(0f, 20f), new Vec2(20f, 20f),
            new Vec2(20f, 10f), new Vec2(0f, 10f),
            new Vec2(0f, 0f), new Vec2(20f, 0f),
        }, path);
    }

    [Fact]
    public void Rows_share_out_a_height_the_spacing_does_not_divide_never_further_apart_than_it()
    {
        var path = SweepPath.Zigzag(new Vec2(0f, 0f), new Vec2(10f, 25f), rowSpacing: 10f);

        // Three gaps of 25/3, not two of 10 and one of 5.
        Assert.Equal(new[] { 25f, 25f, 16.667f, 16.667f, 8.333f, 8.333f, 0f, 0f },
            path.Select(p => (float)System.Math.Round(p.Y, 3)));
    }

    [Fact]
    public void A_box_no_taller_than_a_point_is_swept_in_one_row()
    {
        var path = SweepPath.Zigzag(new Vec2(5f, 7f), new Vec2(15f, 7f), rowSpacing: 10f);

        Assert.Equal(new[] { new Vec2(5f, 7f), new Vec2(15f, 7f) }, path);
    }
}
