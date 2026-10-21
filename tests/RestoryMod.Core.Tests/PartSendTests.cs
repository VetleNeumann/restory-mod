using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class PartSendTests
{
    [Theory]
    [InlineData(DpadDirection.Up, Destination.WashingBin)]
    [InlineData(DpadDirection.Right, Destination.ToolCup)]
    [InlineData(DpadDirection.Left, Destination.PartsBox)]
    [InlineData(DpadDirection.Down, Destination.TrashCan)]
    public void Each_direction_sends_to_its_destination(DpadDirection direction, Destination expected)
    {
        Assert.Equal(expected, PartSend.DestinationFor(direction, shredderOwned: false));
    }

    [Fact]
    public void Down_sends_to_the_shredder_once_owned_since_it_stands_in_the_trash_cans_place()
    {
        Assert.Equal(Destination.Shredder, PartSend.DestinationFor(DpadDirection.Down, shredderOwned: true));
        Assert.Equal(Destination.WashingBin, PartSend.DestinationFor(DpadDirection.Up, shredderOwned: true));
    }

    private static readonly HeldPart Damaged = new(Damaged: true, QuestItem: false, NeedsCleaning: false);

    [Theory]
    [InlineData(Destination.TrashCan, true)]
    [InlineData(Destination.Shredder, true)]
    [InlineData(Destination.PartsBox, false)]
    [InlineData(Destination.WashingBin, false)]
    [InlineData(Destination.ToolCup, false)]
    public void Damaged_part_can_only_be_thrown_away(Destination destination, bool accepted)
    {
        Assert.Equal(accepted, PartSend.Accepts(destination, Damaged));
    }

    [Fact]
    public void Refusing_a_damaged_part_for_the_parts_box_shows_the_games_broken_part_warning()
    {
        Assert.True(PartSend.WarnsBrokenPart(Destination.PartsBox, Damaged));
        Assert.False(PartSend.WarnsBrokenPart(Destination.WashingBin, Damaged));
        Assert.False(PartSend.WarnsBrokenPart(Destination.PartsBox, Clean));
    }

    private static readonly HeldPart Clean = new(Damaged: false, QuestItem: false, NeedsCleaning: false);
    private static readonly HeldPart Dirty = new(Damaged: false, QuestItem: false, NeedsCleaning: true) { Washable = true, FromPlacedDevice = true };

    [Fact]
    public void Tool_cup_takes_only_parts_that_need_cleaning_or_soldering()
    {
        Assert.True(PartSend.Accepts(Destination.ToolCup, Dirty));
        Assert.False(PartSend.Accepts(Destination.ToolCup, Clean));
    }

    [Fact]
    public void Tool_cup_refuses_a_part_from_a_device_other_than_the_one_on_the_bench()
    {
        // Such as a part taken out of the washing bin after the device was swapped.
        Assert.False(PartSend.Accepts(Destination.ToolCup, Dirty with { FromPlacedDevice = false }));
    }

    [Fact]
    public void Undamaged_part_goes_to_the_parts_box_whether_clean_or_dirty()
    {
        Assert.True(PartSend.Accepts(Destination.PartsBox, Clean));
        Assert.True(PartSend.Accepts(Destination.PartsBox, Dirty));
    }

    [Fact]
    public void Washing_bin_refuses_a_part_with_nothing_left_to_wash()
    {
        // Sending it would only waste a place in the bath; the game shows no circle for it either.
        Assert.True(PartSend.Accepts(Destination.WashingBin, Dirty));
        Assert.False(PartSend.Accepts(Destination.WashingBin, Clean));
        Assert.False(PartSend.Accepts(Destination.WashingBin, Dirty with { Washable = false }));
    }

    [Theory]
    [InlineData(DpadDirection.Left, false, Destination.PartsBox)]
    [InlineData(DpadDirection.Down, false, Destination.TrashCan)]
    [InlineData(DpadDirection.Down, true, Destination.Shredder)]
    public void Away_from_the_workbench_left_and_down_send_carried_objects(
        DpadDirection direction, bool shredderOwned, Destination expected)
    {
        Assert.Equal(expected, PartSend.CarriedDestinationFor(direction, shredderOwned));
    }

    [Theory]
    [InlineData(DpadDirection.Up)]
    [InlineData(DpadDirection.Right)]
    public void Away_from_the_workbench_up_and_right_send_nothing(DpadDirection direction)
    {
        // The washing bin and tool cup only work on parts at the workbench.
        Assert.Null(PartSend.CarriedDestinationFor(direction, shredderOwned: false));
    }

    [Fact]
    public void Rubbish_is_thrown_away_and_bought_parts_go_to_the_parts_box()
    {
        var rubbish = new CarriedObject(Rubbish: true, HoldsParts: false);
        var delivery = new CarriedObject(Rubbish: false, HoldsParts: true);
        var device = new CarriedObject(Rubbish: false, HoldsParts: false);

        Assert.True(PartSend.Accepts(Destination.TrashCan, rubbish));
        Assert.True(PartSend.Accepts(Destination.Shredder, rubbish));
        Assert.False(PartSend.Accepts(Destination.PartsBox, rubbish));

        Assert.True(PartSend.Accepts(Destination.PartsBox, delivery));
        Assert.False(PartSend.Accepts(Destination.TrashCan, delivery));

        Assert.False(PartSend.Accepts(Destination.PartsBox, device));
        Assert.False(PartSend.Accepts(Destination.TrashCan, device));
    }

    [Theory]
    [InlineData(DpadDirection.Up, true)]
    [InlineData(DpadDirection.Right, true)]
    [InlineData(DpadDirection.Down, true)]
    [InlineData(DpadDirection.Left, false)]
    public void Holding_a_parts_box_slot_sends_on_any_direction_but_back_to_the_box(DpadDirection direction, bool sends)
    {
        // The part never left the box, so there is nothing to put back.
        Assert.Equal(sends, PartSend.SendsFromHeldSlot(direction));
    }

    [Fact]
    public void Left_puts_a_part_still_shown_as_a_box_icon_back_in_its_slot()
    {
        Assert.True(PartSend.PutsBackInSlot(DpadDirection.Left, shownAsIcon: true));
        Assert.False(PartSend.PutsBackInSlot(DpadDirection.Left, shownAsIcon: false));
        Assert.False(PartSend.PutsBackInSlot(DpadDirection.Up, shownAsIcon: true));
    }

    [Theory]
    [InlineData(Destination.PartsBox)]
    [InlineData(Destination.WashingBin)]
    public void Quest_item_is_refused_by_the_parts_box_and_washing_bin(Destination destination)
    {
        Assert.False(PartSend.Accepts(destination, new HeldPart(Damaged: false, QuestItem: true, NeedsCleaning: true)));
    }

    [Theory]
    [InlineData(1, 1, false)] // just pulled out, or bought for it: a socket is open
    [InlineData(2, 2, false)]
    [InlineData(3, 2, true)]  // a third battery for a device that takes two
    [InlineData(1, 0, true)]  // nothing in the device takes it, such as a part of another device
    public void A_part_is_surplus_when_the_device_on_the_bench_has_no_open_socket_left_for_it(
        int looseCopies, int openSockets, bool surplus)
    {
        Assert.Equal(surplus, PartSend.IsSurplus(looseCopies, openSockets));
    }
}
