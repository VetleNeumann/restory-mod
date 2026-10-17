using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class GamePickTests
{
    private static RayHit<string> Slot(string name, float distance) => new(RayHitLayer.SlotOrGhost, distance, name);
    private static RayHit<string> Part(string? name, float distance) => new(RayHitLayer.PartOrDevice, distance, name);
    private static RayHit<string> Other(string? name, float distance) => new(RayHitLayer.Other, distance, name);

    [Fact]
    public void A_screw_slot_anywhere_along_the_ray_wins_even_behind_a_part()
    {
        Assert.Equal("slot", GamePick.AtWorkbench([Part("casing", 1), Slot("slot", 2)]));
    }

    [Fact]
    public void The_first_slot_in_the_order_physics_returned_wins_not_the_nearest()
    {
        Assert.Equal("far slot", GamePick.AtWorkbench([Slot("far slot", 3), Slot("near slot", 1)]));
    }

    [Fact]
    public void Without_a_slot_the_nearest_part_or_device_wins_over_nearer_bench_equipment()
    {
        Assert.Equal("near chip", GamePick.AtWorkbench([Other("cup", 0.5f), Part("far chip", 2), Part("near chip", 1)]));
        Assert.Null(GamePick.AtWorkbench([Other("cup", 0.5f), Part("chip", 2), Part(null, 1)])); // the device body
    }

    [Fact]
    public void With_no_part_or_device_on_the_ray_the_nearest_other_thing_wins()
    {
        Assert.Equal("near", GamePick.AtWorkbench([Other("far", 2), Other("near", 1)]));
        Assert.Null(GamePick.AtWorkbench(new List<RayHit<string>>()));
    }
}
