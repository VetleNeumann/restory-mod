using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class SendGuidesTests
{
    private static readonly HeldPart Dirty =
        new(Damaged: false, QuestItem: false, NeedsCleaning: true) { Washable = true, FromPlacedDevice = true };

    private static bool AllReady(Destination _) => true;
    private static bool NoneCircled(Destination _) => false;

    [Fact]
    public void Held_dirty_part_is_guided_to_every_destination_that_takes_it_each_with_its_direction()
    {
        Assert.Equal(
            [
                new SendGuide(Destination.WashingBin, DpadDirection.Up),
                new SendGuide(Destination.ToolCup, DpadDirection.Right),
                new SendGuide(Destination.PartsBox, DpadDirection.Left),
            ],
            SendGuides.For(Dirty with { Surplus = true }, shredderOwned: false, AllReady, NoneCircled));
    }

    [Fact]
    public void Parts_box_guides_a_part_the_device_still_has_room_for_only_when_the_game_circles_it_itself()
    {
        // Every part picked up would otherwise blink the parts box.
        Assert.DoesNotContain(SendGuides.For(Dirty, shredderOwned: false, AllReady, NoneCircled),
            g => g.Destination == Destination.PartsBox);
        Assert.Contains(SendGuides.For(Dirty, shredderOwned: false, AllReady, d => d == Destination.PartsBox),
            g => g.Destination == Destination.PartsBox);
    }

    [Fact]
    public void A_destination_whose_own_state_refuses_shows_no_guide()
    {
        // A full or running washing bin would refuse the send.
        Assert.DoesNotContain(SendGuides.For(Dirty, shredderOwned: false, d => d != Destination.WashingBin, NoneCircled),
            g => g.Destination == Destination.WashingBin);
    }

    [Theory]
    [InlineData(false, Destination.TrashCan)]
    [InlineData(true, Destination.Shredder)]
    public void Damaged_part_is_guided_down_to_whichever_of_trash_can_and_shredder_stands_there(
        bool shredderOwned, Destination expected)
    {
        var damaged = new HeldPart(Damaged: true, QuestItem: false, NeedsCleaning: false);
        Assert.Equal([new SendGuide(expected, DpadDirection.Down)], SendGuides.For(damaged, shredderOwned, AllReady, NoneCircled));
    }

    [Fact]
    public void Away_from_the_workbench_bought_parts_are_guided_left_and_rubbish_down()
    {
        var boughtParts = new CarriedObject(Rubbish: false, HoldsParts: true);
        var rubbish = new CarriedObject(Rubbish: true, HoldsParts: false);
        var device = new CarriedObject(Rubbish: false, HoldsParts: false);

        Assert.Equal([new SendGuide(Destination.PartsBox, DpadDirection.Left)],
            SendGuides.For(boughtParts, shredderOwned: false, AllReady));
        Assert.Equal([new SendGuide(Destination.Shredder, DpadDirection.Down)],
            SendGuides.For(rubbish, shredderOwned: true, AllReady));
        Assert.Empty(SendGuides.For(device, shredderOwned: false, AllReady));
    }
}
