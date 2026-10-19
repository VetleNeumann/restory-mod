using System;
using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.Core;

/// <summary>
/// Which parts the workbench hint points out as ready: the parts the player can act on right now.
/// </summary>
public static class ReadyParts
{
    /// <summary>
    /// A part worth taking out: one the player hasn't seen yet (its real condition stays hidden), or
    /// one they know needs cleaning or replacing.
    /// </summary>
    public static bool IsTarget(bool inspected, PartCondition condition) =>
        !inspected || condition != PartCondition.Perfect;

    /// <summary>
    /// Taking a device apart: the installed parts that aren't blocked and are, or stand in the way
    /// of, a target part. Returned in <paramref name="sockets"/> order.
    /// </summary>
    public static List<T> TakingApart<T>(IEnumerable<T> sockets, Func<T, bool> isTarget,
        Func<T, IEnumerable<T>> blockers, Func<T, bool> occupied, Func<T, bool> isBlocked)
    {
        var all = sockets.ToList();
        var inTheWay = new HashSet<T>();
        var pending = new Stack<T>(all.Where(s => occupied(s) && isTarget(s)));
        while (pending.Count > 0)
        {
            var socket = pending.Pop();
            if (!inTheWay.Add(socket)) continue;
            foreach (var blocker in blockers(socket))
            {
                if (occupied(blocker)) pending.Push(blocker);
            }
        }
        return all.Where(s => inTheWay.Contains(s) && !isBlocked(s)).ToList();
    }

    /// <summary>
    /// Putting a device back together: which of the sockets that can take a part now are missing
    /// sockets, pairing each loose part on the bench with one socket of its type (the parts box's
    /// rule, over these sockets only). Returns indices into <paramref name="readySocketTypes"/>.
    /// </summary>
    public static HashSet<int> MissingSockets<T>(IReadOnlyList<T> readySocketTypes, IEnumerable<T> looseTypes)
        where T : class
    {
        var missing = PartsBoxOrder.FindMissing(readySocketTypes, looseTypes);
        var picked = new HashSet<int>();
        for (var i = readySocketTypes.Count - 1; i >= 0; i--)
        {
            var type = readySocketTypes[i];
            if (!missing.TryGetValue(type, out var left) || left == 0) continue;
            missing[type] = left - 1;
            picked.Add(i);
        }
        return picked;
    }
}
