using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class ThingKindTests
{
    [Fact]
    public void A_part_off_its_socket_is_a_loose_part()
    {
        Assert.Equal(ThingKind.LoosePart, ThingKinds.OfPart(inWashingBin: false, inSocket: false, small: false, blocked: false, switchable: false));
    }

    [Fact]
    public void A_small_part_in_a_socket_is_a_screw_even_while_blocked()
    {
        Assert.Equal(ThingKind.Screw, ThingKinds.OfPart(inWashingBin: false, inSocket: true, small: true, blocked: true, switchable: false));
    }

    [Fact]
    public void An_attached_part_that_cannot_come_off_is_blocked_unless_a_press_works_it()
    {
        Assert.Equal(ThingKind.AttachedPart, ThingKinds.OfPart(inWashingBin: false, inSocket: true, small: false, blocked: false, switchable: false));
        Assert.Equal(ThingKind.BlockedPart, ThingKinds.OfPart(inWashingBin: false, inSocket: true, small: false, blocked: true, switchable: false));
        Assert.Equal(ThingKind.Switch, ThingKinds.OfPart(inWashingBin: false, inSocket: true, small: false, blocked: true, switchable: true));
    }

    [Fact]
    public void A_part_in_the_washing_bin_is_a_washing_bin_part()
    {
        Assert.Equal(ThingKind.WashingBinPart, ThingKinds.OfPart(inWashingBin: true, inSocket: false, small: true, blocked: false, switchable: false));
    }

    [Theory]
    [InlineData(ThingKind.Screw, true)]
    [InlineData(ThingKind.ScrewSlot, true)]
    [InlineData(ThingKind.LoosePart, true)]
    [InlineData(ThingKind.AttachedPart, true)]
    [InlineData(ThingKind.Switch, true)]
    [InlineData(ThingKind.BlockedPart, false)]
    [InlineData(ThingKind.PartGhost, false)]
    [InlineData(ThingKind.WashingBinPart, false)]
    [InlineData(ThingKind.DeviceBody, false)]
    [InlineData(ThingKind.Equipment, false)]
    public void Only_things_a_press_works_through_hovering_get_the_minimum_hitbox_size(ThingKind kind, bool enlargeable)
    {
        Assert.Equal(enlargeable, kind.Enlargeable());
    }
}
