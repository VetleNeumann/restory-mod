using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class ReadyPartsTests
{
    private sealed class Socket
    {
        public Socket(string name) => Name = name;

        public string Name { get; }
        public bool Occupied { get; set; } = true;
        public bool Target { get; set; }
        public List<Socket> Blockers { get; } = new();

        // Mirrors the game: a part is blocked while any of its blockers still holds a part.
        public bool Blocked => Occupied && Blockers.Any(b => b.Occupied);

        public override string ToString() => Name;
    }

    private static List<string> Ready(params Socket[] sockets) =>
        ReadyParts.TakingApart(sockets, s => s.Target, s => s.Blockers, s => s.Occupied, s => s.Blocked)
            .Select(s => s.Name).ToList();

    [Fact]
    public void Unblocked_target_is_ready()
    {
        var battery = new Socket("battery") { Target = true };
        Assert.Equal(new[] { "battery" }, Ready(battery));
    }

    [Fact]
    public void Only_the_outermost_part_of_a_chain_in_front_of_a_target_is_ready()
    {
        // Screw holds the lid, the lid covers the board: only the screw can come out now.
        var screw = new Socket("screw");
        var lid = new Socket("lid") { Blockers = { screw } };
        var board = new Socket("board") { Target = true, Blockers = { lid } };
        Assert.Equal(new[] { "screw" }, Ready(board, lid, screw));
    }

    [Fact]
    public void Part_in_the_way_of_no_target_is_not_ready()
    {
        var knob = new Socket("knob");
        var board = new Socket("board") { Target = true };
        Assert.Equal(new[] { "board" }, Ready(knob, board));
    }

    [Fact]
    public void Empty_blocker_does_not_count()
    {
        // Its screw already out: the lid itself is ready, the empty screw socket is not.
        var screw = new Socket("screw") { Occupied = false };
        var lid = new Socket("lid") { Blockers = { screw } };
        var board = new Socket("board") { Target = true, Blockers = { lid } };
        Assert.Equal(new[] { "lid" }, Ready(board, lid, screw));
    }

    [Fact]
    public void Blocker_shared_by_two_targets_is_listed_once()
    {
        var screw = new Socket("screw");
        var left = new Socket("left") { Target = true, Blockers = { screw } };
        var right = new Socket("right") { Target = true, Blockers = { screw } };
        Assert.Equal(new[] { "screw" }, Ready(left, right, screw));
    }

    [Fact]
    public void Sockets_blocking_each_other_do_not_loop()
    {
        var a = new Socket("a") { Target = true };
        var b = new Socket("b") { Blockers = { a } };
        a.Blockers.Add(b);
        Assert.Empty(Ready(a, b));
    }

    [Theory]
    [InlineData(false, PartCondition.Perfect, true)] // unknown: the player hasn't seen it's perfect
    [InlineData(true, PartCondition.Perfect, false)]
    [InlineData(true, PartCondition.Dirty, true)]
    [InlineData(true, PartCondition.Burnt, true)]
    [InlineData(true, PartCondition.Damaged, true)]
    public void Target_is_an_unknown_part_or_one_known_to_need_work(bool inspected, PartCondition condition, bool target)
    {
        Assert.Equal(target, ReadyParts.IsTarget(inspected, condition));
    }

    [Fact]
    public void Ready_socket_with_no_loose_part_for_it_is_missing()
    {
        var missing = ReadyParts.MissingSockets(new[] { "battery", "screen" }, new[] { "screen" });
        Assert.Equal(new[] { 0 }, missing);
    }

    [Fact]
    public void Two_ready_sockets_and_one_loose_part_leave_one_missing()
    {
        var missing = ReadyParts.MissingSockets(new[] { "cap", "screen", "cap" }, new[] { "cap", "screen" });
        Assert.Single(missing);
        Assert.Contains(missing.Single(), new[] { 0, 2 });
    }
}
