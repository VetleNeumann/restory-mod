using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class HintTests
{
    // Part types: sockets and loose parts match on these by reference, as the game's ElementInfo.
    private static readonly object Screw = "screw type", Lid = "lid type", Board = "board type", Cap = "cap type",
        Battery = "battery type";

    private static BenchPart<string> Part(string name, object type, PartCondition? condition = PartCondition.Perfect) =>
        new(name, type, isScrew: type == Screw) { Condition = condition };

    private static BenchSocket<string> Socket(string name, object type, BenchPart<string>? part = null) =>
        new(name, type, takesScrew: type == Screw) { Part = part };

    private static List<string> TakingApart(params BenchSocket<string>[] sockets) =>
        Hint.Plan(new Bench<string>(sockets), takingApart: true).Select(i => $"{i.Kind} {i.Target}").ToList();

    private static List<string> PuttingBack(BenchSocket<string>[] sockets, params BenchPart<string>[] loose) =>
        Hint.Plan(new Bench<string>(sockets, loose), takingApart: false)
            .Select(i => $"{i.Kind} {i.Target}{(i.Waiting ? " waiting" : "")}").ToList();

    // A part is blocked while any of its blockers still holds a part, as in the game.
    private static void Block(BenchSocket<string> socket, params BenchSocket<string>[] blockers)
    {
        socket.Blockers.AddRange(blockers);
        socket.Blocked = blockers.Any(b => b.Part is not null);
    }

    [Fact]
    public void Taking_apart_an_unblocked_part_needing_work_is_ready()
    {
        var board = Socket("board", Board, Part("b", Board, PartCondition.Burnt));
        Assert.Equal(new[] { "Part board" }, TakingApart(board));
    }

    [Fact]
    public void Taking_apart_only_the_outermost_part_in_front_of_a_target_is_ready()
    {
        // Screw holds the lid, the lid covers the board: only the screw can come out now.
        var screw = Socket("screw", Screw, Part("s", Screw));
        var lid = Socket("lid", Lid, Part("l", Lid));
        var board = Socket("board", Board, Part("b", Board, PartCondition.Dirty));
        Block(lid, screw);
        Block(board, lid);
        Assert.Equal(new[] { "Screw screw" }, TakingApart(board, lid, screw));
    }

    [Fact]
    public void Taking_apart_a_part_in_the_way_of_no_target_is_not_ready()
    {
        var knob = Socket("knob", Lid, Part("k", Lid));
        var board = Socket("board", Board, Part("b", Board, PartCondition.Damaged));
        Assert.Equal(new[] { "Part board" }, TakingApart(knob, board));
    }

    [Fact]
    public void Taking_apart_an_empty_blocker_does_not_count()
    {
        // Its screw already out: the lid itself is ready, the empty screw socket is not.
        var screw = Socket("screw", Screw);
        var lid = Socket("lid", Lid, Part("l", Lid));
        var board = Socket("board", Board, Part("b", Board, PartCondition.Dirty));
        Block(lid, screw);
        Block(board, lid);
        Assert.Equal(new[] { "Part lid" }, TakingApart(board, lid, screw));
    }

    [Fact]
    public void Taking_apart_a_blocker_shared_by_two_targets_is_listed_once()
    {
        var screw = Socket("screw", Screw, Part("s", Screw));
        var left = Socket("left", Board, Part("l", Board, PartCondition.Dirty));
        var right = Socket("right", Board, Part("r", Board, PartCondition.Dirty));
        Block(left, screw);
        Block(right, screw);
        Assert.Equal(new[] { "Screw screw" }, TakingApart(left, right, screw));
    }

    [Fact]
    public void Taking_apart_sockets_blocking_each_other_do_not_loop()
    {
        var a = Socket("a", Board, Part("a", Board, PartCondition.Dirty));
        var b = Socket("b", Lid, Part("b", Lid));
        Block(b, a);
        Block(a, b);
        Assert.Empty(TakingApart(a, b));
    }

    [Theory]
    [InlineData(null, true)] // unknown part: the player hasn't seen it's perfect
    [InlineData(PartCondition.Perfect, false)]
    [InlineData(PartCondition.Dirty, true)]
    [InlineData(PartCondition.Burnt, true)]
    [InlineData(PartCondition.Damaged, true)]
    public void Taking_apart_a_target_is_an_unknown_part_or_one_known_to_need_work(PartCondition? condition, bool ready)
    {
        var board = Socket("board", Board, Part("b", Board, condition));
        Assert.Equal(ready, TakingApart(board).Count == 1);
    }

    [Fact]
    public void Taking_apart_a_screw_is_never_a_target_itself()
    {
        var screw = Socket("screw", Screw, Part("s", Screw, PartCondition.Dirty));
        Assert.Empty(TakingApart(screw));
    }

    [Fact]
    public void Taking_apart_a_tube_place_comes_out_only_through_the_tube_entry()
    {
        // Batteries: the one deeper in waits behind the one at the entry.
        var entry = Socket("entry", Battery, Part("b1", Battery));
        var deep = Socket("deep", Battery, Part("b2", Battery, PartCondition.Burnt));
        entry.TubePlaces = new List<BenchSocket<string>?> { deep };
        deep.IsTubePlace = true;
        deep.Blocked = true; // the game blocks it while the entry holds a battery
        Assert.Equal(new[] { "Part entry" }, TakingApart(entry, deep));
    }

    // An empty screw slot keeps the screw that came out of it.
    private static BenchSocket<string> Slot(string name) =>
        new(name, Screw, takesScrew: true) { LastPart = Part(name + " screw", Screw) };

    [Fact]
    public void Putting_back_an_empty_screw_slot_is_ready()
    {
        Assert.Equal(new[] { "ScrewSlot slot" }, PuttingBack(new[] { Slot("slot") }));
    }

    [Fact]
    public void Putting_back_a_screw_slot_the_game_closes_is_not_ready()
    {
        var slot = Slot("slot");
        slot.Available = false;
        Assert.Empty(PuttingBack(new[] { slot }));
    }

    [Fact]
    public void Putting_back_a_filled_socket_is_not_ready()
    {
        var lid = Socket("lid", Lid, Part("l", Lid));
        Assert.Empty(PuttingBack(new[] { lid }, Part("spare lid", Lid)));
    }

    [Fact]
    public void Putting_back_a_loose_part_whose_socket_is_free_is_ready()
    {
        var lid = Socket("lid", Lid);
        Assert.Equal(new[] { "LoosePart loose lid" }, PuttingBack(new[] { lid }, Part("loose lid", Lid)));
    }

    [Fact]
    public void Putting_back_a_loose_part_with_no_free_socket_is_not_ready()
    {
        var lid = Socket("lid", Lid);
        lid.Available = false;
        Assert.Empty(PuttingBack(new[] { lid }, Part("loose lid", Lid)));
    }

    [Fact]
    public void Putting_back_a_held_or_lifted_loose_part_is_not_ready()
    {
        var lid = Socket("lid", Lid);
        var held = Part("held lid", Lid);
        held.Dragging = true;
        var lifted = Part("lifted lid", Lid);
        lifted.OnSurface = false;
        Assert.Empty(PuttingBack(new[] { lid }, held, lifted));
    }

    [Fact]
    public void Putting_back_a_free_socket_no_loose_part_can_fill_is_missing()
    {
        var lid = Socket("lid", Lid);
        var board = Socket("board", Board);
        Assert.Equal(new[] { "LoosePart loose lid", "Missing board" },
            PuttingBack(new[] { lid, board }, Part("loose lid", Lid)));
    }

    [Fact]
    public void Putting_back_two_free_sockets_and_one_loose_part_leave_one_missing()
    {
        var caps = PuttingBack(new[] { Socket("cap 1", Cap), Socket("cap 2", Cap) }, Part("loose cap", Cap));
        Assert.Equal(2, caps.Count);
        Assert.Equal("LoosePart loose cap", caps[0]);
        Assert.StartsWith("Missing cap ", caps[1]);
    }

    [Fact]
    public void Putting_back_a_held_part_still_fills_its_socket_so_none_is_missing()
    {
        var held = Part("held lid", Lid);
        held.Dragging = true;
        Assert.Empty(PuttingBack(new[] { Socket("lid", Lid) }, held));
    }

    [Fact]
    public void Putting_back_parts_wait_while_a_screw_can_go_in()
    {
        Assert.Equal(new[] { "ScrewSlot slot", "LoosePart loose lid waiting", "Missing board waiting" },
            PuttingBack(new[] { Slot("slot"), Socket("lid", Lid), Socket("board", Board) }, Part("loose lid", Lid)));
    }

    [Fact]
    public void Putting_back_an_empty_screw_socket_that_lost_its_screw_is_not_a_screw_slot()
    {
        var socket = Socket("screw socket", Screw);
        Assert.Empty(PuttingBack(new[] { socket }));
    }

    // A battery tube: its entry socket and the places behind it.
    private static (BenchSocket<string> Entry, BenchSocket<string>[] Places) Tube(int behind)
    {
        var entry = Socket("tube", Battery);
        var places = Enumerable.Range(1, behind).Select(i => Socket($"place {i}", Battery)).ToArray();
        foreach (var place in places) place.IsTubePlace = true;
        entry.TubePlaces = places.Cast<BenchSocket<string>?>().ToList();
        return (entry, places);
    }

    [Fact]
    public void Putting_back_an_empty_tube_takes_a_part_for_each_place()
    {
        var (entry, places) = Tube(behind: 2);
        var loose = new[] { Part("b1", Battery), Part("b2", Battery), Part("b3", Battery), Part("b4", Battery) };
        var plan = PuttingBack(places.Prepend(entry).ToArray(), loose);
        Assert.Equal(new[] { "LoosePart b1", "LoosePart b2", "LoosePart b3", "LoosePart b4" }, plan);
    }

    [Fact]
    public void Putting_back_a_tube_short_of_batteries_is_missing_once()
    {
        var (entry, places) = Tube(behind: 2);
        Assert.Equal(new[] { "LoosePart b1", "Missing tube" },
            PuttingBack(places.Prepend(entry).ToArray(), Part("b1", Battery)));
    }

    [Fact]
    public void Putting_back_a_tube_with_its_entry_filled_takes_one_per_free_place_behind()
    {
        var (entry, places) = Tube(behind: 2);
        entry.Part = Part("in", Battery);
        entry.Available = false; // the game counts a filled entry as unavailable
        places[0].Part = Part("deep", Battery);
        Assert.Equal(new[] { "Missing tube" }, PuttingBack(places.Prepend(entry).ToArray()));
    }

    [Fact]
    public void Putting_back_a_filled_tube_waits_for_the_sockets_it_closes_off_to_be_done()
    {
        var (entry, places) = Tube(behind: 1);
        entry.Part = Part("in", Battery);
        var cover = Socket("cover", Lid, Part("c", Lid));
        cover.Part!.Progress = 0.5f; // still under repair
        entry.Closes.Add(cover);
        Assert.Empty(PuttingBack(places.Prepend(entry).Append(cover).ToArray()));

        cover.Part.Progress = 0f;
        Assert.Equal(new[] { "Missing tube" }, PuttingBack(places.Prepend(entry).Append(cover).ToArray()));
    }
}
