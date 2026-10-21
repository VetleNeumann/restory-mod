using System;
using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.Core;

/// <summary>
/// The game's hint pulse as the ghost ledger drives it. A spot is what a ghost sits on (a socket, or a
/// loose part); the pulse keeps the ghosts, and the marker is the game's own screw-slot marker there.
/// Where it returns "null", it must be a real null, never a destroyed engine object: the ledger
/// compares references.
/// </summary>
public interface IGhostPulse<TSpot, TGhost> where TGhost : class
{
    /// <summary>The newest ghost the pulse shows on this spot, or null.</summary>
    TGhost? GhostUnder(TSpot spot);

    /// <summary>Asks the pulse for a ghost of this kind's look; null when the pulse made none (it just ended).</summary>
    TGhost? Create(TSpot spot, HintKind kind);

    /// <summary>The ghost is still in the pulse.</summary>
    bool Alive(TGhost ghost);

    /// <summary>Takes a ghost out of the pulse; false if it was no longer in it.</summary>
    bool Destroy(TGhost ghost);

    /// <summary>Turns a fading pulse back up.</summary>
    void RiseAgain();

    /// <summary>Switches off the game's marker on this spot so it can't hide the ghost; the marker, or null.</summary>
    TGhost? MuteMarker(TSpot spot);

    void Unmute(TGhost marker);

    /// <summary>Gives a ghost the hint's own look at the pulse's current brightness.</summary>
    void Paint(TGhost ghost, Look look);

    /// <summary>Gives a ghost the pulse's plain look back.</summary>
    void Unpaint(TGhost ghost);

    /// <summary>
    /// Moves a ghost the hint tracks, a screw's too, to the hint's own outlines, drawn one by one so
    /// overlapping ghosts stay apart.
    /// </summary>
    void Claim(TGhost ghost);

    /// <summary>Hands a claimed ghost back to the game's outlines.</summary>
    void Release(TGhost ghost);
}

/// <summary>
/// Every ghost the hint shows, one per spot, and the one way any ghost joins the pulse: the hint's own
/// ghosts through <see cref="Show"/>, the game's (its drop hint, its own hint button) through
/// <see cref="GameCreating"/>. Only the hint's own ghosts are tracked: they wear the hint's look, mute
/// the game's marker on a screw slot, and leave once the plan no longer wants them.
/// </summary>
public sealed class GhostLedger<TSpot, TGhost> where TSpot : notnull where TGhost : class
{
    private sealed class Entry
    {
        public Entry(TGhost ghost, HintKind kind)
        {
            Ghost = ghost;
            Kind = kind;
        }

        public TGhost Ghost { get; }
        public HintKind Kind { get; set; }
        public Look Look { get; set; }
        public TGhost? Muted { get; set; }
    }

    private readonly IGhostPulse<TSpot, TGhost> pulse;
    private readonly Func<bool> tidy;
    private readonly Dictionary<TSpot, Entry> entries = new();
    private int asked;
    private bool creating;

    /// <param name="tidy">
    /// [Workbench] TidyHintPulse: the game gets no second ghost on a spot, and a fading pulse rises
    /// again when its ghosts are asked for.
    /// </param>
    public GhostLedger(IGhostPulse<TSpot, TGhost> pulse, Func<bool> tidy)
    {
        this.pulse = pulse;
        this.tidy = tidy;
    }

    /// <summary>The hint shows some ghost of its own.</summary>
    public bool Any => entries.Count > 0;

    /// <summary>Shows a ghost for this plan item, or takes over the one already on its spot.</summary>
    public void Show(HintItem<TSpot> item)
    {
        asked++;
        var ghost = pulse.GhostUnder(item.Target);
        if (ghost is not null)
        {
            // As the game's own create would: a ghost already there still lifts a fading pulse.
            pulse.RiseAgain();
        }
        else
        {
            if (tidy()) pulse.RiseAgain();
            creating = true;
            try
            {
                ghost = pulse.Create(item.Target, item.Kind);
            }
            finally
            {
                creating = false;
            }
            if (ghost is null) return;
        }

        if (entries.TryGetValue(item.Target, out var entry) && entry.Ghost != ghost)
        {
            Forget(entry);
            entry = null;
        }
        if (entry is null)
        {
            entry = new Entry(ghost, item.Kind);
            entries[item.Target] = entry;
            pulse.Claim(ghost);
        }
        Restyle(item.Target, entry, item);
    }

    /// <summary>
    /// The game is about to make a ghost on this spot (its create, hooked); returns whether to let it.
    /// </summary>
    public bool GameCreating(TSpot spot)
    {
        if (creating) return true; // this ledger's own create, already counted
        asked++;
        if (!tidy()) return true;
        pulse.RiseAgain();
        return pulse.GhostUnder(spot) is null;
    }

    /// <summary>Runs a hint and tells whether it asked for any ghost, made or already there.</summary>
    public bool ShowsAny(Action hint)
    {
        var before = asked;
        hint();
        return asked != before;
    }

    /// <summary>
    /// Brings the hint's ghosts in line with a fresh plan: ghosts it no longer wants leave. Adds none:
    /// new ghosts come only when the hint is asked for.
    /// </summary>
    public void Reconcile(IEnumerable<HintItem<TSpot>> plan)
    {
        var wanted = new Dictionary<TSpot, HintItem<TSpot>>();
        foreach (var item in plan) wanted[item.Target] = item;
        foreach (var (spot, entry) in entries.ToList())
        {
            if (wanted.TryGetValue(spot, out var item) && item.Kind == entry.Kind)
            {
                // A part's condition can come to light while its ghost stays.
                entry.Look = Looks.OfHint(item.Kind, item.Condition);
                continue;
            }
            entries.Remove(spot);
            Forget(entry);
            pulse.Destroy(entry.Ghost);
        }
    }

    /// <summary>Every frame: forgets ghosts the pulse dropped and keeps the rest in step with it.</summary>
    public void Tick()
    {
        foreach (var (spot, entry) in entries.ToList())
        {
            bool alive;
            try
            {
                alive = pulse.Alive(entry.Ghost);
                if (alive) pulse.Paint(entry.Ghost, entry.Look);
            }
            catch (Exception)
            {
                alive = false; // its spot or ghost went with the device
            }
            if (alive) continue;
            entries.Remove(spot);
            Forget(entry);
        }
    }

    /// <summary>The spot changed (a part went in or out): every ghost on it leaves, the game's too.</summary>
    public void Drop(TSpot spot)
    {
        if (entries.TryGetValue(spot, out var entry))
        {
            entries.Remove(spot);
            Forget(entry);
        }
        while (pulse.GhostUnder(spot) is { } ghost && pulse.Destroy(ghost)) { }
    }

    /// <summary>The pulse ended and is about to pool its ghosts: they get the pulse's look back first.</summary>
    public void PulseEnded() => ForgetAll();

    /// <summary>On unload: hands every ghost back to the plain pulse.</summary>
    public void Unload() => ForgetAll();

    /// <summary>
    /// After a hot reload: takes over the ghosts the pulse still shows that the plan wants, as they
    /// were before; the pulse neither rises nor counts them. The rest stay the game's.
    /// </summary>
    public void Adopt(IEnumerable<HintItem<TSpot>> plan)
    {
        foreach (var item in plan)
        {
            if (entries.ContainsKey(item.Target) || pulse.GhostUnder(item.Target) is not { } ghost) continue;
            var entry = new Entry(ghost, item.Kind);
            entries[item.Target] = entry;
            pulse.Claim(ghost);
            Restyle(item.Target, entry, item);
        }
    }

    private void Restyle(TSpot spot, Entry entry, HintItem<TSpot> item)
    {
        entry.Kind = item.Kind;
        entry.Look = Looks.OfHint(item.Kind, item.Condition);
        if (item.Kind == HintKind.ScrewSlot && entry.Muted is null) entry.Muted = pulse.MuteMarker(spot);
        else if (item.Kind != HintKind.ScrewSlot && entry.Muted is not null)
        {
            pulse.Unmute(entry.Muted);
            entry.Muted = null;
        }
        pulse.Paint(entry.Ghost, entry.Look);
    }

    private void ForgetAll()
    {
        foreach (var entry in entries.Values) Forget(entry);
        entries.Clear();
    }

    private void Forget(Entry entry)
    {
        if (entry.Muted is not null) pulse.Unmute(entry.Muted);
        pulse.Unpaint(entry.Ghost);
        pulse.Release(entry.Ghost);
    }
}
