using System.Collections.Generic;

namespace RestoryMod.Core;

public enum PartCondition
{
    Damaged,
    Dirty,
    Perfect,
    Other,
}

/// <summary>Everything the parts box sort needs to know about one slot.</summary>
public readonly struct PartsBoxKey
{
    public PartsBoxKey(bool missing, int reassembleRank, PartCondition condition, int slotIndex)
    {
        Missing = missing;
        ReassembleRank = reassembleRank;
        Condition = condition;
        SlotIndex = slotIndex;
    }

    /// <summary>The part fills a socket the "How to reassemble" list marks as missing.</summary>
    public bool Missing { get; }

    /// <summary>Position in the "How to reassemble" list, or <see cref="PartsBoxOrder.NotInDevice"/>.</summary>
    public int ReassembleRank { get; }

    public PartCondition Condition { get; }

    /// <summary>The game's storage slot index; used as the final tie-break (highest first, like the game).</summary>
    public int SlotIndex { get; }
}

/// <summary>
/// Parts box ordering while a device is on the workbench:
/// 1. Missing parts section before other parts (never reversed).
/// 2. "How to reassemble" order (reversed by the game's sort toggle); parts the device doesn't use go last.
/// 3. Condition: damaged, dirty, perfect (never reversed).
/// 4. Slot index, highest first (the game's own tie-break).
/// </summary>
public static class PartsBoxOrder
{
    public const int NotInDevice = int.MaxValue;

    /// <summary>
    /// Mirrors the game's notepad rule: each empty socket pairs with one loose part of the same type
    /// on the workbench; unpaired empty sockets are missing. The parts box is not consulted.
    /// </summary>
    public static HashSet<T> FindMissing<T>(IEnumerable<T> emptySocketTypes, IEnumerable<T> looseTypes)
        where T : class
    {
        var loose = new List<T>(looseTypes);
        var missing = new HashSet<T>();
        foreach (var socketType in emptySocketTypes)
        {
            var index = loose.IndexOf(socketType);
            if (index >= 0)
                loose.RemoveAt(index);
            else
                missing.Add(socketType);
        }
        return missing;
    }

    /// <summary>Rank of each part type by its first socket in the "How to reassemble" list.</summary>
    public static Dictionary<T, int> RanksFromSocketOrder<T>(IEnumerable<T> socketTypesInListOrder)
        where T : class
    {
        var ranks = new Dictionary<T, int>();
        var rank = 0;
        foreach (var type in socketTypesInListOrder)
        {
            if (!ranks.ContainsKey(type))
                ranks[type] = rank++;
        }
        return ranks;
    }

    public static int Compare(in PartsBoxKey a, in PartsBoxKey b, bool reverseReassembleOrder)
    {
        if (a.Missing != b.Missing)
            return a.Missing ? -1 : 1;

        if (a.ReassembleRank != b.ReassembleRank)
        {
            if (a.ReassembleRank == NotInDevice) return 1;
            if (b.ReassembleRank == NotInDevice) return -1;
            return reverseReassembleOrder
                ? b.ReassembleRank.CompareTo(a.ReassembleRank)
                : a.ReassembleRank.CompareTo(b.ReassembleRank);
        }

        if (a.Condition != b.Condition)
            return a.Condition.CompareTo(b.Condition);

        return b.SlotIndex.CompareTo(a.SlotIndex);
    }
}
