using RestoryMod.E2E;

namespace RestoryMod.Core.Tests;

public class BoxListingTests
{
    private const string Missing = "RestoryMod_Header_Missing_parts";
    private const string Other = "RestoryMod_Header_Other_parts";

    [Fact]
    public void Each_slot_falls_under_the_header_above_it()
    {
        var rows = new[] { Missing, "Speaker", "Button", Other, "Lid" };

        var sections = BoxListing.Sections(rows, Missing, Other);

        Assert.Equal(new[] { ("Speaker", BoxSection.Missing), ("Button", BoxSection.Missing), ("Lid", BoxSection.Other) },
            sections);
    }
}
