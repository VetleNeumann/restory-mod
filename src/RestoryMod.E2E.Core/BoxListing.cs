using System.Collections.Generic;

namespace RestoryMod.E2E;

/// <summary>Where a slot sits in the parts box list (CONTEXT.md, Parts box).</summary>
public enum BoxSection
{
    /// <summary>The list has no headers: nothing is missing, or the sections are off.</summary>
    None,
    Missing,
    Other,
}

/// <summary>Reads the parts box list as shown: each slot under the header above it.</summary>
public static class BoxListing
{
    /// <summary>
    /// Each slot of <paramref name="rows"/> (the list's children in order, headers among them) with the
    /// section it falls under.
    /// </summary>
    public static List<(string slot, BoxSection section)> Sections(IEnumerable<string> rows, string missingHeader,
        string otherHeader)
    {
        var sections = new List<(string, BoxSection)>();
        var under = BoxSection.None;
        foreach (var row in rows)
        {
            if (row == missingHeader) under = BoxSection.Missing;
            else if (row == otherHeader) under = BoxSection.Other;
            else sections.Add((row, under));
        }
        return sections;
    }
}
