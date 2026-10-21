using System.Collections.Generic;

namespace RestoryMod.E2E;

/// <summary>The visible treatment one thing at the workbench carries (see CONTEXT.md, Mark).</summary>
public enum Mark
{
    None,

    /// <summary>The hint's outline on a thing that is there: an attached part, a screw, a loose part.</summary>
    Pulse,

    /// <summary>The hint's dim, steady outline on a waiting part.</summary>
    FaintPulse,

    /// <summary>The hint's yellow part ghost in an empty socket: a screw slot whose screw can go in now.</summary>
    Ghost,

    /// <summary>The hint's red part ghost in a missing socket.</summary>
    RedGhost,

    /// <summary>The game's own outline on the thing under the virtual cursor.</summary>
    HoverOutline,
}

/// <summary>One thing whose mark is not the expected one; <see cref="Actual"/> null: not at the workbench.</summary>
public sealed record MarkMismatch(string Thing, Mark Expected, Mark? Actual);

public static class MarkCheck
{
    /// <summary>
    /// Compares the marks a check expects with the marks things carry now. An exact check also expects
    /// <see cref="Mark.None"/> on every thing it does not name; a subset check looks only at the things it
    /// names.
    /// </summary>
    public static IReadOnlyList<MarkMismatch> Compare(IReadOnlyDictionary<string, Mark> expected,
        IReadOnlyDictionary<string, Mark> actual, bool exact)
    {
        var mismatches = new List<MarkMismatch>();
        foreach (var pair in actual)
        {
            var want = expected.TryGetValue(pair.Key, out var named) ? named : Mark.None;
            if ((exact || expected.ContainsKey(pair.Key)) && want != pair.Value)
                mismatches.Add(new MarkMismatch(pair.Key, want, pair.Value));
        }
        foreach (var pair in expected)
            if (!actual.ContainsKey(pair.Key)) mismatches.Add(new MarkMismatch(pair.Key, pair.Value, null));
        return mismatches;
    }
}
