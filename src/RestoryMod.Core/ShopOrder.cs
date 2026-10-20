using System;
using System.Collections.Generic;
using System.Linq;

namespace RestoryMod.Core;

/// <summary>Everything the parts store sort needs to know about one product.</summary>
public readonly struct ShopKey
{
    public ShopKey(int model, int step)
    {
        Model = model;
        Step = step;
    }

    /// <summary>Position of the part's device model in the store's model dropdown, or <see cref="ShopOrder.NoModel"/>.</summary>
    public int Model { get; }

    /// <summary>Rank in that model's reassembly order, or <see cref="ShopOrder.NoStep"/>.</summary>
    public int Step { get; }
}

/// <summary>
/// Parts store ordering:
/// 1. Device model, in the store's model dropdown order; parts of no model go last (never reversed).
/// 2. The model's reassembly order (reversed by the game's sort toggle); parts that are no step go last.
/// 3. The game's own order.
/// </summary>
public static class ShopOrder
{
    public const int NoModel = int.MaxValue;
    public const int NoStep = int.MaxValue;

    /// <summary>Sorts the products; a stable sort, so ties keep the order they came in.</summary>
    public static List<T> Arrange<T>(IEnumerable<T> items, Func<T, ShopKey> keyOf, bool reverse)
    {
        var byModel = items
            .Select(item => (item, key: keyOf(item)))
            .OrderBy(entry => entry.key.Model)
            .ThenBy(entry => entry.key.Step == NoStep);
        var ordered = reverse
            ? byModel.ThenByDescending(entry => entry.key.Step)
            : byModel.ThenBy(entry => entry.key.Step);
        return ordered.Select(entry => entry.item).ToList();
    }

    /// <summary>
    /// Rank of each part in a model's reassembly order, from the model's part list. Mirrors the game's
    /// notepad, which lists the parts last to first.
    /// </summary>
    public static Dictionary<T, int> StepRanks<T>(IReadOnlyList<T> partsInListOrder, Func<T, bool> isStep)
        where T : class
    {
        var firstListings = new List<T>();
        var seen = new HashSet<T>();
        foreach (var part in partsInListOrder)
        {
            if (seen.Add(part) && isStep(part))
                firstListings.Add(part);
        }

        var ranks = new Dictionary<T, int>();
        for (var i = firstListings.Count - 1; i >= 0; i--)
            ranks[firstListings[i]] = ranks.Count;
        return ranks;
    }
}
