using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.Core;

/// <summary>A part as the hint sees it: installed in a socket, or loose on the bench.</summary>
/// <typeparam name="T">The game object the part stands for.</typeparam>
public sealed class BenchPart<T>
{
    public BenchPart(T handle, object type, bool isScrew)
    {
        Handle = handle;
        Type = type;
        IsScrew = isScrew;
    }

    public T Handle { get; }

    /// <summary>The part's type, compared by reference with what a socket takes.</summary>
    public object Type { get; }

    public bool IsScrew { get; }

    /// <summary>Its condition, or null for an unknown part (never read from the game while unknown).</summary>
    public PartCondition? Condition { get; set; }

    /// <summary>How far through a repair it is; a part in a tube's way is done at zero or less.</summary>
    public float Progress { get; set; }

    public bool OnSurface { get; set; } = true;
    public bool Dragging { get; set; }
}

/// <summary>A socket on the device at the workbench.</summary>
/// <typeparam name="T">The game object the socket stands for.</typeparam>
public sealed class BenchSocket<T>
{
    public BenchSocket(T handle, object? takes, bool takesScrew)
    {
        Handle = handle;
        Takes = takes;
        TakesScrew = takesScrew;
    }

    public T Handle { get; }

    /// <summary>The part type it takes; null when it takes none.</summary>
    public object? Takes { get; }

    public bool TakesScrew { get; }

    /// <summary>The part in it now.</summary>
    public BenchPart<T>? Part { get; set; }

    /// <summary>The part it last held, which the game keeps paired with it (a removed screw).</summary>
    public BenchPart<T>? LastPart { get; set; }

    /// <summary>The game won't let its part come off yet.</summary>
    public bool Blocked { get; set; }

    /// <summary>The game would let a part go in now.</summary>
    public bool Available { get; set; } = true;

    /// <summary>Sockets whose parts stand in the way of this one's.</summary>
    public List<BenchSocket<T>> Blockers { get; } = new();

    /// <summary>Sockets this one closes off once filled (null where the game's object is gone).</summary>
    public List<BenchSocket<T>?> Closes { get; set; } = new();

    /// <summary>For a tube (batteries): the places behind its entry, deepest last; null otherwise.</summary>
    public List<BenchSocket<T>?>? TubePlaces { get; set; }

    /// <summary>One of a tube's places behind its entry: it takes nothing directly.</summary>
    public bool IsTubePlace { get; set; }
}

/// <summary>The device's sockets and the loose parts on the bench.</summary>
public sealed class Bench<T>
{
    public Bench(IEnumerable<BenchSocket<T>> sockets, IEnumerable<BenchPart<T>>? loose = null)
    {
        Sockets = sockets.ToList();
        Loose = loose?.ToList() ?? new List<BenchPart<T>>();
    }

    public IReadOnlyList<BenchSocket<T>> Sockets { get; }
    public IReadOnlyList<BenchPart<T>> Loose { get; }
}

public enum HintKind
{
    /// <summary>Taking apart: an installed part that can come out now.</summary>
    Part,

    /// <summary>Taking apart: an installed screw that can come out now.</summary>
    Screw,

    /// <summary>Putting back: a screw slot whose screw can go in now.</summary>
    ScrewSlot,

    /// <summary>Putting back: a loose part whose socket can take it now.</summary>
    LoosePart,

    /// <summary>Putting back: a missing socket.</summary>
    Missing,
}

/// <summary>
/// One thing the hint points out. <see cref="Target"/> is the socket, or the loose part for
/// <see cref="HintKind.LoosePart"/>.
/// </summary>
public readonly record struct HintItem<T>(T Target, HintKind Kind);

/// <summary>What the workbench hint points out: every ready part and missing socket.</summary>
public static class Hint
{
    /// <summary>
    /// The hint for the game's mode. Taking apart with no part left that needs work or is unknown, the
    /// device is done: the hint shows putting back, though the game only switches once a part goes in.
    /// </summary>
    public static List<HintItem<T>> Plan<T>(Bench<T> bench, bool takingApart) =>
        takingApart && bench.Sockets.Any(IsTarget) ? TakingApart(bench) : PuttingBack(bench);

    private static List<HintItem<T>> TakingApart<T>(Bench<T> bench)
    {
        // A part deeper in a tube (batteries) comes out only through the tube's entry.
        var entries = new Dictionary<BenchSocket<T>, BenchSocket<T>>();
        foreach (var tube in bench.Sockets.Where(s => s.TubePlaces is not null))
        {
            foreach (var place in tube.TubePlaces!)
            {
                if (place is not null) entries[place] = tube;
            }
        }
        IEnumerable<BenchSocket<T>> Blockers(BenchSocket<T> socket) =>
            entries.TryGetValue(socket, out var entry) ? socket.Blockers.Append(entry) : socket.Blockers;

        // Every installed part that is, or stands in the way of, a target; then those that can come out now.
        var inTheWay = new HashSet<BenchSocket<T>>();
        var pending = new Stack<BenchSocket<T>>(bench.Sockets.Where(IsTarget));
        while (pending.Count > 0)
        {
            var socket = pending.Pop();
            if (!inTheWay.Add(socket)) continue;
            foreach (var blocker in Blockers(socket))
            {
                if (blocker.Part is not null) pending.Push(blocker);
            }
        }
        return bench.Sockets.Where(s => inTheWay.Contains(s) && !s.Blocked)
            .Select(s => new HintItem<T>(s.Handle, s.Part!.IsScrew ? HintKind.Screw : HintKind.Part))
            .ToList();
    }

    /// <summary>
    /// Screw slots that can take their screw now, loose parts that can go in now, and missing sockets.
    /// </summary>
    private static List<HintItem<T>> PuttingBack<T>(Bench<T> bench)
    {
        var ready = bench.Sockets.Where(s => Room(s) > 0).ToList();
        var items = ready.Where(IsScrewSlot).Select(s => new HintItem<T>(s.Handle, HintKind.ScrewSlot)).ToList();

        // A tube (batteries) counts once per part it still takes, so each pairs with one loose part.
        var sockets = ready.Where(s => !s.TakesScrew).SelectMany(s => Enumerable.Repeat(s, Room(s))).ToList();
        var types = sockets.Select(s => s.Takes!).ToList();
        items.AddRange(bench.Loose
            .Where(p => p.OnSurface && !p.Dragging && types.Contains(p.Type))
            .Select(p => new HintItem<T>(p.Handle, HintKind.LoosePart)));

        // Each loose part counts toward one socket of its type (the parts box's rule, over these sockets
        // only); a held part still counts, as it is on its way to one.
        var missing = PartsBoxOrder.FindMissing(types, bench.Loose.Select(p => p.Type));
        var picked = new List<BenchSocket<T>>();
        for (var i = sockets.Count - 1; i >= 0; i--)
        {
            if (!missing.TryGetValue(types[i], out var left) || left == 0) continue;
            missing[types[i]] = left - 1;
            if (!picked.Contains(sockets[i])) picked.Insert(0, sockets[i]);
        }
        items.AddRange(picked.Select(s => new HintItem<T>(s.Handle, HintKind.Missing)));
        return items;
    }

    // An empty screw slot whose screw can go back in now (the game keeps the removed screw paired with it).
    private static bool IsScrewSlot<T>(BenchSocket<T> socket) => socket.TakesScrew && socket.LastPart is { IsScrew: true };

    /// <summary>
    /// How many more parts a socket takes now. One for a free socket the game would fill. A tube takes
    /// as many as it has free places: each part pushed in moves the one before it deeper, so its entry
    /// is taken while places behind it are still free. The places behind take nothing directly.
    /// </summary>
    private static int Room<T>(BenchSocket<T> socket)
    {
        if (socket.Takes is null || socket.IsTubePlace) return 0;
        if (socket.TubePlaces is null) return socket.Part is null && socket.Available ? 1 : 0;
        var behind = socket.TubePlaces.Count(place => place is { Part: null });
        if (socket.Part is null) return socket.Available ? 1 + behind : 0;
        // The rest of Available: every socket the tube closes off is filled and done.
        return behind > 0 && socket.Closes.All(b => b?.Part is { Progress: <= 0f }) ? behind : 0;
    }

    // An unknown part, or one known to need cleaning or replacing. Screws hold parts; they don't get repaired.
    private static bool IsTarget<T>(BenchSocket<T> socket) =>
        socket.Part is { IsScrew: false } part && part.Condition != PartCondition.Perfect;
}
