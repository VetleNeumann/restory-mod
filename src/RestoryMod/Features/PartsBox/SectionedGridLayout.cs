using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoryMod.Features.PartsBox;

/// <summary>
/// A grid like the game's GridLayoutGroup (fixed columns, filled from the top), except that header
/// children take a full row of their own, short height. Enabled only while sections are shown, with
/// the game's grid disabled; GridLayoutGroup forces every cell to the same size, so headers can't
/// live in it.
/// <para>
/// Implements the layout interfaces directly instead of deriving from LayoutGroup: LayoutGroup is
/// [DisallowMultipleComponent], so a subclass can't sit next to the game's GridLayoutGroup.
/// </para>
/// </summary>
internal sealed class SectionedGridLayout : UIBehaviour, ILayoutGroup, ILayoutElement
{
    public const string HeaderPrefix = "RestoryMod_Header";

    public Vector2 CellSize;
    public Vector2 Spacing;
    public int Columns = 1;
    public RectOffset Padding = new();
    public TextAnchor Alignment = TextAnchor.UpperLeft;
    public float HeaderHeight = 60f;
    public float HeaderGap = 8f;

    private readonly List<RectTransform> children = new();
    private DrivenRectTransformTracker tracker;
    private float contentHeight;

    private RectTransform Rect => (RectTransform)transform;

    private float RowWidth => Columns * CellSize.x + (Columns - 1) * Spacing.x;

    /// <summary>Copies the game grid's geometry so items land exactly where they did before.</summary>
    public void CopyFrom(GridLayoutGroup grid, int columns)
    {
        CellSize = grid.cellSize;
        Spacing = grid.spacing;
        Columns = Mathf.Max(1, columns);
        Padding = new RectOffset(grid.padding.left, grid.padding.right, grid.padding.top, grid.padding.bottom);
        Alignment = grid.childAlignment;
        SetDirty();
    }

    // ILayoutElement: report our size so the container's ContentSizeFitter / ScrollRect follow it.
    public float minWidth => Padding.horizontal + RowWidth;
    public float preferredWidth => minWidth;
    public float flexibleWidth => -1;
    public float minHeight => Padding.vertical + contentHeight;
    public float preferredHeight => minHeight;
    public float flexibleHeight => -1;
    public int layoutPriority => 0;

    public void CalculateLayoutInputHorizontal()
    {
        children.Clear();
        foreach (Transform t in transform)
        {
            if (t is not RectTransform rt || !rt.gameObject.activeInHierarchy) continue;
            if (rt.TryGetComponent<ILayoutIgnorer>(out var ignorer) && ignorer.ignoreLayout) continue;
            children.Add(rt);
        }
    }

    public void CalculateLayoutInputVertical() => contentHeight = Place(apply: false);

    public void SetLayoutHorizontal() => Place(apply: true);

    public void SetLayoutVertical() => Place(apply: true);

    /// <summary>Lays children out top to bottom; returns the content height (excluding padding).</summary>
    private float Place(bool apply)
    {
        if (apply) tracker.Clear();

        var available = Rect.rect.width - Padding.horizontal;
        var alignX = (int)Alignment % 3 * 0.5f; // 0 left, 0.5 centre, 1 right
        var startX = Padding.left + (available - RowWidth) * alignX;
        var y = 0f;
        var column = 0;

        foreach (var child in children)
        {
            if (child.name.StartsWith(HeaderPrefix))
            {
                if (column > 0)
                {
                    y += CellSize.y + Spacing.y;
                    column = 0;
                }
                if (apply) PlaceChild(child, startX, Padding.top + y, RowWidth, HeaderHeight);
                y += HeaderHeight + HeaderGap;
                continue;
            }

            if (apply) PlaceChild(child, startX + column * (CellSize.x + Spacing.x), Padding.top + y, CellSize.x, CellSize.y);
            if (++column == Columns)
            {
                column = 0;
                y += CellSize.y + Spacing.y;
            }
        }

        return column > 0 ? y + CellSize.y : Mathf.Max(0, y - Spacing.y);
    }

    private void PlaceChild(RectTransform child, float x, float y, float width, float height)
    {
        tracker.Add(this, child,
            DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
        child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, x, width);
        child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, y, height);
    }

    private void SetDirty()
    {
        if (isActiveAndEnabled) LayoutRebuilder.MarkLayoutForRebuild(Rect);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetDirty();
    }

    protected override void OnDisable()
    {
        tracker.Clear();
        LayoutRebuilder.MarkLayoutForRebuild(Rect);
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange() => SetDirty();

    private void OnTransformChildrenChanged() => SetDirty();
}
