using RestoryMod.E2E;

namespace RestoryMod.Core.Tests;

public class MarkCheckTests
{
    private static Dictionary<string, Mark> Marks(params (string thing, Mark mark)[] marks) =>
        marks.ToDictionary(m => m.thing, m => m.mark);

    [Fact]
    public void Exact_check_reports_a_stray_mark_on_a_thing_it_did_not_name()
    {
        var expected = Marks(("Back cover", Mark.Pulse));
        var actual = Marks(("Back cover", Mark.Pulse), ("Screw 1", Mark.Pulse), ("Battery", Mark.None));

        var mismatches = MarkCheck.Compare(expected, actual, exact: true);

        Assert.Equal(new[] { new MarkMismatch("Screw 1", Mark.None, Mark.Pulse) }, mismatches);
    }

    [Fact]
    public void Subset_check_looks_only_at_the_things_it_names()
    {
        var expected = Marks(("Battery", Mark.RedGhost));
        var actual = Marks(("Back cover", Mark.Pulse), ("Battery", Mark.FaintPulse));

        var mismatches = MarkCheck.Compare(expected, actual, exact: false);

        Assert.Equal(new[] { new MarkMismatch("Battery", Mark.RedGhost, Mark.FaintPulse) }, mismatches);
    }

    [Fact]
    public void A_named_thing_that_is_not_at_the_workbench_is_reported_absent()
    {
        var expected = Marks(("Battery", Mark.None), ("Fuse", Mark.Ghost));
        var actual = Marks(("Back cover", Mark.None));

        var mismatches = MarkCheck.Compare(expected, actual, exact: false);

        Assert.Equal(new[]
        {
            new MarkMismatch("Battery", Mark.None, null),
            new MarkMismatch("Fuse", Mark.Ghost, null),
        }, mismatches);
    }
}
