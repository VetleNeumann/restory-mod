using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class GhostLedgerTests
{
    private sealed class Ghost
    {
        public Ghost(string under) => Under = under;
        public string Under { get; }
        public bool Painted { get; set; }
        public Look Look { get; set; }
        public bool Claimed { get; set; }
        public override string ToString() => Under;
    }

    /// <summary>The game's pulse as the ledger sees it: ghosts by the spot they sit on.</summary>
    private sealed class FakePulse : IGhostPulse<string, Ghost>
    {
        public readonly List<Ghost> Ghosts = new();
        public readonly HashSet<string> Markers = new();
        public readonly HashSet<string> Muted = new();
        public int Rises;
        public bool Ending;

        public Ghost? GhostUnder(string spot) => Ghosts.LastOrDefault(g => g.Under == spot);

        public Ghost? Create(string spot, HintKind kind)
        {
            if (Ending) return null;
            var ghost = new Ghost(spot);
            Ghosts.Add(ghost);
            return ghost;
        }

        public bool Alive(Ghost ghost) => Ghosts.Contains(ghost);
        public bool Destroy(Ghost ghost) => Ghosts.Remove(ghost);
        public void RiseAgain() => Rises++;

        public Ghost? MuteMarker(string spot)
        {
            if (!Markers.Contains(spot)) return null;
            Muted.Add(spot);
            return new Ghost(spot);
        }

        public void Unmute(Ghost marker) => Muted.Remove(marker.Under);

        public void Paint(Ghost ghost, Look look)
        {
            ghost.Painted = true;
            ghost.Look = look;
        }

        public void Unpaint(Ghost ghost) => ghost.Painted = false;
        public void Claim(Ghost ghost) => ghost.Claimed = true;
        public void Release(Ghost ghost) => ghost.Claimed = false;
    }

    private readonly FakePulse pulse = new();
    private bool tidy = true;
    private GhostLedger<string, Ghost> ledger;

    public GhostLedgerTests() => ledger = new GhostLedger<string, Ghost>(pulse, () => tidy);

    private static HintItem<string> Item(string spot, HintKind kind) => new(spot, kind);

    [Fact]
    public void Showing_a_spot_twice_keeps_one_ghost()
    {
        ledger.Show(Item("lid", HintKind.Part));
        ledger.Show(Item("lid", HintKind.Part));
        Assert.Single(pulse.Ghosts);
    }

    [Fact]
    public void Showing_a_spot_the_game_already_ghosts_takes_that_ghost_over()
    {
        Assert.True(ledger.GameCreating("screw"));
        pulse.Create("screw", HintKind.Screw);
        ledger.Show(Item("screw", HintKind.Screw));
        Assert.Single(pulse.Ghosts);

        ledger.Reconcile(Array.Empty<HintItem<string>>());
        Assert.Empty(pulse.Ghosts);
    }

    [Fact]
    public void The_game_gets_no_second_ghost_on_a_spot_while_tidy()
    {
        ledger.Show(Item("screw", HintKind.Screw));
        Assert.False(ledger.GameCreating("screw"));
        Assert.True(ledger.GameCreating("other screw"));

        tidy = false;
        Assert.True(ledger.GameCreating("screw"));
    }

    [Fact]
    public void A_fading_pulse_rises_again_when_a_ghost_is_asked_for()
    {
        ledger.Show(Item("lid", HintKind.Part));
        ledger.Show(Item("lid", HintKind.Part));
        ledger.GameCreating("screw");
        Assert.Equal(3, pulse.Rises);
    }

    [Fact]
    public void Untidy_only_spots_already_ghosted_rise_the_pulse()
    {
        tidy = false;
        ledger.Show(Item("lid", HintKind.Part));
        ledger.GameCreating("screw");
        Assert.Equal(0, pulse.Rises);
        ledger.Show(Item("lid", HintKind.Part));
        Assert.Equal(1, pulse.Rises);
    }

    [Fact]
    public void Shows_any_tells_whether_a_hint_asked_for_a_ghost()
    {
        Assert.False(ledger.ShowsAny(() => { }));
        Assert.True(ledger.ShowsAny(() => ledger.Show(Item("lid", HintKind.Part))));
        // Asking again for a ghost already there still counts: the hint showed it.
        Assert.True(ledger.ShowsAny(() => ledger.Show(Item("lid", HintKind.Part))));
        Assert.True(ledger.ShowsAny(() => ledger.GameCreating("screw")));
    }

    [Fact]
    public void Shows_any_counts_its_own_create_once()
    {
        // The ledger's create goes through the game's, which reports back to the ledger.
        var nested = 0;
        var reporting = new ReportingPulse(pulse, spot => { if (ledger!.GameCreating(spot)) nested++; });
        var ledgerOverReporting = new GhostLedger<string, Ghost>(reporting, () => tidy);
        ledger = ledgerOverReporting;
        Assert.True(ledger.ShowsAny(() => ledger.Show(Item("lid", HintKind.Part))));
        Assert.Equal(1, nested);
        Assert.Single(pulse.Ghosts);
        Assert.Equal(1, pulse.Rises);
    }

    [Theory]
    [InlineData(HintKind.Part, Look.Ready)]
    [InlineData(HintKind.LoosePart, Look.Ready)]
    [InlineData(HintKind.Missing, Look.Missing)]
    [InlineData(HintKind.Screw, Look.Screw)]
    [InlineData(HintKind.ScrewSlot, Look.Screw)]
    public void Every_ghost_the_hint_shows_wears_its_look(HintKind kind, Look look)
    {
        ledger.Show(Item("spot", kind));
        var ghost = pulse.Ghosts.Single();
        Assert.True(ghost.Painted);
        Assert.Equal(look, ghost.Look);
    }

    [Fact]
    public void Missing_sockets_wear_the_missing_look()
    {
        ledger.Show(Item("board", HintKind.Missing));
        Assert.Equal(Look.Missing, pulse.Ghosts.Single().Look);
    }

    [Theory]
    [InlineData(HintKind.Part)]
    [InlineData(HintKind.Screw)]
    [InlineData(HintKind.ScrewSlot)]
    public void Every_ghost_the_hint_tracks_is_claimed_until_it_goes(HintKind kind)
    {
        ledger.Show(Item("spot", kind));
        var ghost = pulse.Ghosts.Single();
        Assert.True(ghost.Claimed);

        ledger.Reconcile(Array.Empty<HintItem<string>>());
        Assert.False(ghost.Claimed);
    }

    [Fact]
    public void The_game_own_ghosts_are_never_claimed()
    {
        ledger.GameCreating("screw");
        pulse.Create("screw", HintKind.Screw);
        ledger.Tick();
        Assert.False(pulse.Ghosts.Single().Claimed);
    }

    [Fact]
    public void Ghosts_left_to_the_game_when_the_pulse_ends_are_released()
    {
        ledger.Show(Item("lid", HintKind.Part));
        ledger.Show(Item("screw", HintKind.Screw));
        ledger.PulseEnded();
        Assert.All(pulse.Ghosts, g => Assert.False(g.Claimed));
    }

    [Fact]
    public void A_screw_slot_mutes_the_game_marker_on_it_until_its_ghost_goes()
    {
        pulse.Markers.Add("slot");
        ledger.Show(Item("slot", HintKind.ScrewSlot));
        Assert.Contains("slot", pulse.Muted);

        ledger.Reconcile(Array.Empty<HintItem<string>>());
        Assert.Empty(pulse.Muted);
    }

    [Fact]
    public void Reconcile_drops_ghosts_no_longer_ready_and_restores_their_look()
    {
        ledger.Show(Item("lid", HintKind.Part));
        ledger.Show(Item("board", HintKind.Part));
        var lid = pulse.Ghosts[0];

        ledger.Reconcile(new[] { Item("board", HintKind.Part) });
        Assert.Equal(new[] { "board" }, pulse.Ghosts.Select(g => g.Under));
        Assert.False(lid.Painted);
    }

    [Fact]
    public void Reconcile_leaves_the_game_own_ghosts_alone()
    {
        ledger.GameCreating("screw");
        pulse.Create("screw", HintKind.Screw);
        ledger.Reconcile(Array.Empty<HintItem<string>>());
        Assert.Single(pulse.Ghosts);
    }

    [Fact]
    public void Reconcile_adds_nothing()
    {
        ledger.Reconcile(new[] { Item("lid", HintKind.Part) });
        Assert.Empty(pulse.Ghosts);
    }

    [Fact]
    public void A_ghost_the_pulse_dropped_is_forgotten_and_its_marker_restored()
    {
        pulse.Markers.Add("slot");
        ledger.Show(Item("slot", HintKind.ScrewSlot));
        pulse.Ghosts.Clear();
        ledger.Tick();
        Assert.Empty(pulse.Muted);
        Assert.False(ledger.Any);
    }

    [Fact]
    public void Dropping_a_spot_removes_every_ghost_on_it()
    {
        tidy = false;
        ledger.Show(Item("socket", HintKind.Screw));
        pulse.Create("socket", HintKind.Screw); // the game's own, stacked while untidy
        ledger.Drop("socket");
        Assert.Empty(pulse.Ghosts);
        Assert.False(ledger.Any);
    }

    [Fact]
    public void When_the_pulse_ends_ghosts_get_the_pulse_look_back_and_are_left_to_the_game()
    {
        pulse.Markers.Add("slot");
        ledger.Show(Item("lid", HintKind.Part));
        ledger.Show(Item("slot", HintKind.ScrewSlot));
        ledger.PulseEnded();
        Assert.Equal(2, pulse.Ghosts.Count); // the game pools them itself
        Assert.All(pulse.Ghosts, g => Assert.False(g.Painted));
        Assert.Empty(pulse.Muted);
        Assert.False(ledger.Any);
    }

    [Fact]
    public void A_pulse_that_just_ended_makes_no_ghost()
    {
        pulse.Ending = true;
        ledger.Show(Item("lid", HintKind.Part));
        Assert.False(ledger.Any);
    }

    [Fact]
    public void Adopting_takes_over_live_ghosts_the_plan_still_wants()
    {
        // After a hot reload: the game still shows these ghosts, plain.
        pulse.Markers.Add("slot");
        pulse.Create("lid", HintKind.Part);
        pulse.Create("slot", HintKind.ScrewSlot);
        pulse.Create("screw", HintKind.Screw); // the game's own drop hint

        ledger.Adopt(new[] { Item("lid", HintKind.Part), Item("slot", HintKind.ScrewSlot), Item("board", HintKind.Part) });

        Assert.Equal(3, pulse.Ghosts.Count); // none made, none dropped
        Assert.True(pulse.Ghosts[0].Painted);
        Assert.Contains("slot", pulse.Muted);
        Assert.Equal(0, pulse.Rises);
        ledger.Reconcile(Array.Empty<HintItem<string>>());
        Assert.Equal(new[] { "screw" }, pulse.Ghosts.Select(g => g.Under));
    }

    /// <summary>A pulse whose create reports to the ledger first, as the game's create does through its patch.</summary>
    private sealed class ReportingPulse : IGhostPulse<string, Ghost>
    {
        private readonly FakePulse inner;
        private readonly Action<string> report;

        public ReportingPulse(FakePulse inner, Action<string> report)
        {
            this.inner = inner;
            this.report = report;
        }

        public Ghost? GhostUnder(string spot) => inner.GhostUnder(spot);

        public Ghost? Create(string spot, HintKind kind)
        {
            report(spot);
            return inner.Create(spot, kind);
        }

        public bool Alive(Ghost ghost) => inner.Alive(ghost);
        public bool Destroy(Ghost ghost) => inner.Destroy(ghost);
        public void RiseAgain() => inner.RiseAgain();
        public Ghost? MuteMarker(string spot) => inner.MuteMarker(spot);
        public void Unmute(Ghost marker) => inner.Unmute(marker);
        public void Paint(Ghost ghost, Look look) => inner.Paint(ghost, look);
        public void Unpaint(Ghost ghost) => inner.Unpaint(ghost);
        public void Claim(Ghost ghost) => inner.Claim(ghost);
        public void Release(Ghost ghost) => inner.Release(ghost);
    }
}
