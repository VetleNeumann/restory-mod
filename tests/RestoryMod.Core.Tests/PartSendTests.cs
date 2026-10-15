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

    private static readonly HeldPart Clean = new(Damaged: false, QuestItem: false, NeedsCleaning: false);
    private static readonly HeldPart Dirty = new(Damaged: false, QuestItem: false, NeedsCleaning: true);

    [Fact]
    public void Tool_cup_takes_only_parts_that_need_cleaning_or_soldering()
    {
        Assert.True(PartSend.Accepts(Destination.ToolCup, Dirty));
        Assert.False(PartSend.Accepts(Destination.ToolCup, Clean));
    }

    [Theory]
    [InlineData(Destination.PartsBox)]
    [InlineData(Destination.WashingBin)]
    public void Undamaged_part_goes_to_the_parts_box_or_washing_bin_whether_clean_or_dirty(Destination destination)
    {
        Assert.True(PartSend.Accepts(destination, Clean));
        Assert.True(PartSend.Accepts(destination, Dirty));
    }

    [Theory]
    [InlineData(Destination.PartsBox)]
    [InlineData(Destination.WashingBin)]
    public void Quest_item_is_refused_by_the_parts_box_and_washing_bin(Destination destination)
    {
        Assert.False(PartSend.Accepts(destination, new HeldPart(Damaged: false, QuestItem: true, NeedsCleaning: true)));
    }
}
