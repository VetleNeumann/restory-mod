using System.Numerics;
using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class HitboxTests
{
    /// <summary>A native hitbox made of one axis-aligned rectangle, as screen pixels.</summary>
    private static Hitbox Rect(float x, float y, float width, float height) =>
        Hitbox.FromShapes([[new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)]]);

    /// <summary>A rectangle centred on (cx, cy), turned by <paramref name="degrees"/>.</summary>
    private static Vector2[] Turned(float cx, float cy, float width, float height, float degrees)
    {
        var turn = Matrix3x2.CreateRotation(degrees * MathF.PI / 180) * Matrix3x2.CreateTranslation(cx, cy);
        return
        [
            Vector2.Transform(new(-width / 2, -height / 2), turn),
            Vector2.Transform(new(width / 2, -height / 2), turn),
            Vector2.Transform(new(width / 2, height / 2), turn),
            Vector2.Transform(new(-width / 2, height / 2), turn),
        ];
    }

    [Fact]
    public void Narrowest_width_of_a_square_is_its_side()
    {
        Assert.Equal(10, Rect(0, 0, 10, 10).NarrowestWidth, 0.01);
    }

    [Fact]
    public void Narrowest_width_follows_the_things_own_slant()
    {
        // A battery lying at an angle: 40 x 6 px, turned 30 degrees. Its screen bounds are far wider.
        var battery = Hitbox.FromShapes([Turned(100, 100, 40, 6, 30)]);

        Assert.Equal(6, battery.NarrowestWidth, 0.01);
    }

    [Fact]
    public void Distance_is_zero_inside_and_measured_to_the_nearest_edge_or_corner_outside()
    {
        var screw = Rect(0, 0, 10, 10);

        Assert.Equal(0, screw.DistanceTo(new(5, 5)), 0.01);
        Assert.Equal(5, screw.DistanceTo(new(15, 5)), 0.01);
        Assert.Equal(5, screw.DistanceTo(new(13, 14)), 0.01); // 3 right and 4 above the top-right corner
    }

    [Fact]
    public void A_part_with_two_shapes_is_hit_on_them_but_not_in_the_gap_between()
    {
        var bracket = Hitbox.FromShapes(
        [
            [new(0, 0), new(10, 0), new(10, 10), new(0, 10)],
            [new(30, 0), new(40, 0), new(40, 10), new(30, 10)],
        ]);

        Assert.Equal(0, bracket.DistanceTo(new(35, 5)), 0.01);
        Assert.Equal(10, bracket.DistanceTo(new(20, 5)), 0.01);
    }

    [Fact]
    public void Corners_hidden_inside_the_outline_do_not_change_it()
    {
        // A box seen from straight on: its back corners project inside the front face.
        var box = Hitbox.FromShapes(
        [
            [new(0, 0), new(20, 0), new(20, 20), new(0, 20), new(5, 5), new(15, 5), new(15, 15), new(5, 15)],
        ]);

        Assert.Equal(20, box.NarrowestWidth, 0.01);
        Assert.Equal(0, box.DistanceTo(new(2, 2)), 0.01);
    }

    [Fact]
    public void A_small_hitbox_grows_on_each_side_until_its_narrowest_width_reaches_the_minimum()
    {
        Assert.Equal(16.5, Rect(0, 0, 10, 10).GrowthFor(43), 0.01); // round screw head: 43 px across
        Assert.Equal(17, Rect(0, 0, 24, 9).GrowthFor(43), 0.01);    // battery lying flat: about 58 x 43
    }

    [Fact]
    public void A_hitbox_already_at_least_the_minimum_does_not_grow()
    {
        Assert.Equal(0, Rect(0, 0, 60, 43).GrowthFor(43), 0.01);
        Assert.Equal(0, Rect(0, 0, 10, 10).GrowthFor(0), 0.01);
    }

    [Fact]
    public void A_thing_seen_as_a_single_point_grows_to_a_circle_the_minimum_across()
    {
        var speck = Hitbox.FromShapes([[new(50, 50), new(50, 50)]]);

        Assert.Equal(21.5, speck.GrowthFor(43), 0.01);
        Assert.Equal(3, speck.DistanceTo(new(53, 50)), 0.01);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void The_drawn_outline_runs_exactly_along_the_hitbox_edge(float growth)
    {
        // The overlay draws what the picking measures: every outline point sits `growth` px out.
        var battery = Hitbox.FromShapes([Turned(100, 100, 24, 9, 30)]);

        var outline = Assert.Single(battery.Outlines(growth));
        Assert.True(outline.Length >= 4);
        Assert.All(outline, point => Assert.Equal(growth, battery.DistanceTo(point), 0.05));
    }

    [Fact]
    public void A_points_enlarged_outline_is_a_circle_around_it()
    {
        var speck = Hitbox.FromShapes([[new(50, 50)]]);

        var outline = Assert.Single(speck.Outlines(10));
        Assert.True(outline.Length >= 16);
        Assert.All(outline, point => Assert.Equal(10, Vector2.Distance(point, new Vector2(50, 50)), 0.05));
    }
}
