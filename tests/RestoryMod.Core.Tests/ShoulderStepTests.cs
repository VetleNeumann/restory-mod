using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class ShoulderStepTests
{
    [Fact]
    public void Next_moves_to_the_following_entry()
    {
        Assert.Equal(2, ShoulderStep.Cycle(new[] { true, true, true, true }, current: 1, step: 1));
    }

    [Fact]
    public void Next_on_the_last_entry_wraps_to_the_first()
    {
        Assert.Equal(0, ShoulderStep.Cycle(new[] { true, true, true }, current: 2, step: 1));
    }

    [Fact]
    public void Previous_on_the_first_entry_wraps_to_the_last()
    {
        Assert.Equal(2, ShoulderStep.Cycle(new[] { true, true, true }, current: 0, step: -1));
    }

    [Fact]
    public void Hidden_entries_are_skipped()
    {
        // Tool window: tools not owned, and brushes other than the best one, are hidden slots.
        var shown = new[] { true, false, false, true, false };
        Assert.Equal(3, ShoulderStep.Cycle(shown, current: 0, step: 1));
        Assert.Equal(0, ShoulderStep.Cycle(shown, current: 3, step: 1));
        Assert.Equal(3, ShoulderStep.Cycle(shown, current: 0, step: -1));
    }

    [Fact]
    public void With_nothing_current_the_first_entry_counts_as_current()
    {
        // The inventory shows "All", its first toggle, by selecting no toggle at all.
        var shown = new[] { false, true, true, true };
        Assert.Equal(2, ShoulderStep.Cycle(shown, current: -1, step: 1));
        Assert.Equal(3, ShoulderStep.Cycle(shown, current: -1, step: -1));
    }

    [Fact]
    public void A_row_is_ordered_left_to_right()
    {
        // The Tool window lists its slots right to left.
        var positions = new[] { (300f, 50f), (200f, 50f), (100f, 50f) };
        Assert.Equal(new[] { 2, 1, 0 }, ShoulderStep.ScreenOrder(positions));
    }

    [Fact]
    public void A_column_is_ordered_top_to_bottom()
    {
        // Screen y grows upwards.
        var positions = new[] { (40f, 100f), (40.01f, 300f), (39.99f, 200f) };
        Assert.Equal(new[] { 1, 2, 0 }, ShoulderStep.ScreenOrder(positions));
    }

    [Fact]
    public void Stays_put_when_the_current_entry_is_the_only_one()
    {
        Assert.Equal(1, ShoulderStep.Cycle(new[] { false, true, false }, current: 1, step: 1));
    }

    [Fact]
    public void Nothing_to_move_to_when_no_entry_is_selectable()
    {
        Assert.Equal(-1, ShoulderStep.Cycle(new[] { false, false }, current: -1, step: 1));
        Assert.Equal(-1, ShoulderStep.Cycle(new bool[0], current: -1, step: -1));
    }

    [Theory]
    [InlineData(LookDirection.Main, 1, LookDirection.Right)]
    [InlineData(LookDirection.Main, -1, LookDirection.Left)]
    [InlineData(LookDirection.Left, 1, LookDirection.Main)]
    [InlineData(LookDirection.Right, -1, LookDirection.Main)]
    public void Look_steps_one_direction_at_a_time(LookDirection from, int step, LookDirection to)
    {
        Assert.Equal(to, ShoulderStep.Look(from, step));
    }

    [Theory]
    [InlineData(LookDirection.Right, 1)]
    [InlineData(LookDirection.Left, -1)]
    public void Look_stops_at_the_sides(LookDirection side, int step)
    {
        Assert.Equal(side, ShoulderStep.Look(side, step));
    }
}
