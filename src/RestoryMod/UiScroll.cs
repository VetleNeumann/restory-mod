using RestoryMod.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RestoryMod;

internal static class UiScroll
{
    /// <summary>Scrolls <paramref name="scrollRect"/> just enough that <paramref name="row"/> is fully visible.</summary>
    public static void RowIntoView(ScrollRect scrollRect, RectTransform row)
    {
        var viewport = scrollRect.viewport ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        var corners = new Vector3[4];
        row.GetWorldCorners(corners);
        var rowBottom = viewport.InverseTransformPoint(corners[0]).y;
        var rowTop = viewport.InverseTransformPoint(corners[1]).y;
        var view = viewport.rect;

        var shift = ScrollIntoView.ContentShift(rowTop, rowBottom, view.yMax, view.yMin);
        if (shift == 0f) return;
        scrollRect.StopMovement();
        scrollRect.content.anchoredPosition += new Vector2(0f, shift);
    }
}
