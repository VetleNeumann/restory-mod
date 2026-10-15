using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class PartsBoxOrderTests
{
    private const PartCondition Damaged = PartCondition.Damaged;
    private const PartCondition Dirty = PartCondition.Dirty;
    private const PartCondition Perfect = PartCondition.Perfect;

    [Fact]
    public void Empty_socket_with_no_loose_part_is_missing()
    {
        var missing = PartsBoxOrder.FindMissing(new[] { "battery", "screen" }, new[] { "screen" });
        Assert.Equal(new[] { "battery" }, missing);
    }

    [Fact]
    public void Each_loose_part_pairs_with_only_one_empty_socket()
    {
        var missing = PartsBoxOrder.FindMissing(new[] { "cap", "cap" }, new[] { "cap" });
        Assert.Equal(new[] { "cap" }, missing);
    }

    [Fact]
    public void Ranks_follow_first_occurrence_in_list_order()
    {
        var ranks = PartsBoxOrder.RanksFromSocketOrder(new[] { "screen", "cap", "screen", "battery" });
        Assert.Equal(0, ranks["screen"]);
        Assert.Equal(1, ranks["cap"]);
        Assert.Equal(2, ranks["battery"]);
    }

    private static PartsBoxKey Key(bool missing, int rank, PartCondition condition, int slot) =>
        new(missing, rank, condition, slot);

    private static List<string> Sort(Dictionary<string, PartsBoxKey> items, bool reverse) =>
        items.OrderBy(kv => kv.Value, Comparer<PartsBoxKey>.Create((a, b) => PartsBoxOrder.Compare(a, b, reverse)))
             .Select(kv => kv.Key)
             .ToList();

    [Fact]
    public void Missing_section_comes_first_even_when_reversed()
    {
        var items = new Dictionary<string, PartsBoxKey>
        {
            ["other-rank0"] = Key(false, 0, Damaged, 1),
            ["missing-rank5"] = Key(true, 5, Perfect, 2),
        };
        Assert.Equal("missing-rank5", Sort(items, reverse: false)[0]);
        Assert.Equal("missing-rank5", Sort(items, reverse: true)[0]);
    }

    [Fact]
    public void Reassemble_order_is_primary_and_reverses()
    {
        var items = new Dictionary<string, PartsBoxKey>
        {
            ["rank1-perfect"] = Key(false, 1, Perfect, 1),
            ["rank0-perfect"] = Key(false, 0, Perfect, 2),
            ["rank2-damaged"] = Key(false, 2, Damaged, 3),
        };
        Assert.Equal(new[] { "rank0-perfect", "rank1-perfect", "rank2-damaged" }, Sort(items, reverse: false));
        Assert.Equal(new[] { "rank2-damaged", "rank1-perfect", "rank0-perfect" }, Sort(items, reverse: true));
    }

    [Fact]
    public void Condition_is_secondary_and_never_reverses()
    {
        var items = new Dictionary<string, PartsBoxKey>
        {
            ["perfect"] = Key(false, 0, Perfect, 1),
            ["damaged"] = Key(false, 0, Damaged, 2),
            ["dirty"] = Key(false, 0, Dirty, 3),
        };
        var expected = new[] { "damaged", "dirty", "perfect" };
        Assert.Equal(expected, Sort(items, reverse: false));
        Assert.Equal(expected, Sort(items, reverse: true));
    }

    [Fact]
    public void Parts_not_in_device_stay_last_when_reversed()
    {
        var items = new Dictionary<string, PartsBoxKey>
        {
            ["unused"] = Key(false, PartsBoxOrder.NotInDevice, Damaged, 9),
            ["rank0"] = Key(false, 0, Perfect, 1),
            ["rank3"] = Key(false, 3, Perfect, 2),
        };
        Assert.Equal("unused", Sort(items, reverse: false)[^1]);
        Assert.Equal("unused", Sort(items, reverse: true)[^1]);
    }

    [Fact]
    public void Slot_index_breaks_ties_highest_first()
    {
        var items = new Dictionary<string, PartsBoxKey>
        {
            ["slot1"] = Key(false, 0, Dirty, 1),
            ["slot7"] = Key(false, 0, Dirty, 7),
        };
        Assert.Equal(new[] { "slot7", "slot1" }, Sort(items, reverse: false));
        Assert.Equal(new[] { "slot7", "slot1" }, Sort(items, reverse: true));
    }
}
