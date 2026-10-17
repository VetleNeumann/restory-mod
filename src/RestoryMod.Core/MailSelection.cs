using System.Collections.Generic;

namespace RestoryMod.Core;

/// <summary>
/// Which mail is the Selected mail, by index in the open mail folder's on-screen list (top first);
/// -1 for none. See CONTEXT.md: Selected mail.
/// </summary>
public static class MailSelection
{
    /// <summary>
    /// The mail selected after a D-pad press, down (+1) or up (-1), stopping at the ends. With none
    /// selected, either direction selects the top mail.
    /// </summary>
    public static int Step(int count, int current, int step)
    {
        if (count == 0) return -1;
        if (current < 0) return 0;
        return System.Math.Max(0, System.Math.Min(count - 1, current + System.Math.Sign(step)));
    }

    /// <summary>
    /// The mail to select once the open folder's list changes from <paramref name="before"/> to
    /// <paramref name="after"/>: the same mail if it is still there, else the one that takes its place,
    /// else the nearest one above it, else the top mail (mail arriving in an empty folder).
    /// </summary>
    public static int AfterChange<T>(IReadOnlyList<T> before, int selected, IReadOnlyList<T> after)
    {
        if (selected < 0) return after.Count > 0 ? 0 : -1;
        var comparer = EqualityComparer<T>.Default;
        int IndexAfter(T mail)
        {
            for (var i = 0; i < after.Count; i++)
            {
                if (comparer.Equals(after[i], mail)) return i;
            }
            return -1;
        }

        for (var i = selected; i < before.Count; i++)
        {
            var index = IndexAfter(before[i]);
            if (index >= 0) return index;
        }
        for (var i = System.Math.Min(selected, before.Count) - 1; i >= 0; i--)
        {
            var index = IndexAfter(before[i]);
            if (index >= 0) return index;
        }
        return after.Count > 0 ? 0 : -1;
    }
}
