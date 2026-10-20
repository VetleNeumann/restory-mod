using RestoryMod.E2E;

namespace RestoryMod.Core.Tests;

public class StickSteerTests
{
    private static readonly StickSteer Steer = new(tolerance: 4f, slowWithin: 100f, minTilt: 0.25f);

    [Fact]
    public void A_far_target_gets_the_stick_fully_tilted_towards_it()
    {
        var tilt = Steer.Tilt(cursor: new Vec2(100, 100), target: new Vec2(400, 500));

        Assert.Equal(0.6f, tilt.X, precision: 3);
        Assert.Equal(0.8f, tilt.Y, precision: 3);
    }

    [Fact]
    public void Near_the_target_the_tilt_eases_off_with_the_distance()
    {
        var tilt = Steer.Tilt(cursor: new Vec2(100, 100), target: new Vec2(150, 100));

        Assert.Equal(0.5f, tilt.X, precision: 3);
        Assert.Equal(0f, tilt.Y, precision: 3);
    }

    [Fact]
    public void Just_outside_the_tolerance_the_tilt_stays_big_enough_to_move_the_cursor()
    {
        var tilt = Steer.Tilt(cursor: new Vec2(100, 100), target: new Vec2(100, 94));

        Assert.Equal(0f, tilt.X, precision: 3);
        Assert.Equal(-0.25f, tilt.Y, precision: 3);
    }

    [Fact]
    public void Within_the_tolerance_the_stick_rests_and_the_cursor_has_arrived()
    {
        Assert.True(Steer.Arrived(cursor: new Vec2(100, 100), target: new Vec2(103, 102)));
        Assert.Equal(new Vec2(0, 0), Steer.Tilt(cursor: new Vec2(100, 100), target: new Vec2(103, 102)));
        Assert.False(Steer.Arrived(cursor: new Vec2(100, 100), target: new Vec2(100, 94)));
    }
}
