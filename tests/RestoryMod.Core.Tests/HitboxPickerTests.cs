using System.Numerics;
using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class HitboxPickerTests
{
    /// <summary>Minimum hitbox size used throughout: about 4% of a 1080 px screen.</summary>
    private const float Minimum = 43;

    private static HitboxThing<string> Thing(string name, float x, float y, float width, float height,
        bool enlargeable = true, bool seen = true) =>
        new(name, Hitbox.FromShapes([[new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)]]),
            enlargeable, seen);

    private static HitboxPick<string> Pick(Vector2 cursor, string? gamePick, params HitboxThing<string>[] things) =>
        HitboxPicker.Pick(cursor, Minimum, things, gamePick, panelUnderCursor: false);

    [Fact]
    public void In_a_gap_next_to_a_small_screw_the_cursor_hovers_the_screw()
    {
        var screw = Thing("screw", 0, 0, 10, 10);

        var pick = Pick(new(20, 5), gamePick: null, screw); // 10 px right of it; it grows 16.5 px

        Assert.Equal(new HitboxPick<string>("screw", PickReason.Enlarged), pick);
    }

    [Fact]
    public void An_enlarged_screw_beats_the_big_part_under_the_cursor()
    {
        var screw = Thing("screw", 0, 0, 10, 10);
        var board = Thing("board", -100, -100, 300, 300);

        var pick = Pick(new(20, 5), gamePick: "board", screw, board);

        Assert.Equal(new HitboxPick<string>("screw", PickReason.Enlarged), pick);
    }

    [Fact]
    public void A_blocked_part_or_part_ghost_counts_as_empty_space()
    {
        // Neither is ever enlarged, and an enlarged hitbox wins over the native one the game picked.
        var casing = Thing("casing", 12, 0, 20, 20, enlargeable: false);
        var screw = Thing("screw", 0, 0, 10, 10);

        Assert.Equal(new HitboxPick<string>("screw", PickReason.Enlarged), Pick(new(15, 5), "casing", casing, screw));
        Assert.Equal(new HitboxPick<string>(null, PickReason.Native), Pick(new(38, 5), null, casing));
    }

    [Fact]
    public void A_thing_the_camera_cannot_see_keeps_its_native_hitbox()
    {
        // A screw on the device's underside: its spot on screen lies over the front face.
        var hiddenScrew = Thing("screw", 0, 0, 10, 10, seen: false);

        Assert.Equal(new HitboxPick<string>("case", PickReason.Native), Pick(new(15, 5), "case", hiddenScrew));
    }

    [Theory]
    [InlineData(19, "left")]
    [InlineData(21, "right")]
    public void Where_enlarged_hitboxes_overlap_they_split_halfway_between_the_edges(float x, string expected)
    {
        // Two screws 20 px apart, both grown 16.5 px, so their enlarged hitboxes overlap by 13 px.
        var left = Thing("left", 0, 0, 10, 10);
        var right = Thing("right", 30, 0, 10, 10);

        Assert.Equal(expected, Pick(new(x, 5), null, left, right).Thing);
    }

    [Fact]
    public void Nearest_is_measured_to_the_native_outline_not_the_centre()
    {
        // The cursor is 7 px from the battery's end but 9 px from the screw; the screw's centre is nearer.
        var battery = Thing("battery", 0, 0, 24, 9);
        var screw = Thing("screw", 40, 0, 10, 10);

        Assert.Equal("battery", Pick(new(31, 4.5f), null, battery, screw).Thing);
    }

    [Fact]
    public void A_panel_under_the_cursor_always_wins()
    {
        var screw = Thing("screw", 0, 0, 10, 10);

        var pick = HitboxPicker.Pick(new(5, 5), Minimum, [screw], gamePick: null, panelUnderCursor: true);

        Assert.Equal(new HitboxPick<string>(null, PickReason.Panel), pick);
    }

    [Fact]
    public void The_cursor_on_a_small_things_native_hitbox_keeps_it_inside_a_neighbours_enlarged_one()
    {
        var left = Thing("left", 0, 0, 10, 10);
        var right = Thing("right", 14, 0, 10, 10);

        Assert.Equal(new HitboxPick<string>("left", PickReason.Native), Pick(new(9, 5), "left", left, right));
    }

    [Fact]
    public void Where_native_hitboxes_overlap_the_games_pick_wins_then_the_front_most()
    {
        // Listed front to back; the cursor is inside both.
        var front = Thing("front", 0, 0, 10, 10);
        var back = Thing("back", 5, 0, 10, 10);

        Assert.Equal("back", Pick(new(7, 5), "back", front, back).Thing);
        Assert.Equal("front", Pick(new(7, 5), null, front, back).Thing);
    }

    [Fact]
    public void A_thing_at_least_the_minimum_across_is_never_enlarged()
    {
        var board = Thing("board", 0, 0, 60, 50);

        Assert.Equal(new HitboxPick<string>(null, PickReason.Native), Pick(new(65, 25), null, board));
    }

    [Fact]
    public void A_minimum_of_zero_enlarges_nothing()
    {
        var screw = Thing("screw", 0, 0, 10, 10);

        var pick = HitboxPicker.Pick(new(12, 5), 0, [screw], gamePick: null, panelUnderCursor: false);

        Assert.Equal(new HitboxPick<string>(null, PickReason.Native), pick);
    }
}
