using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Devices;
using Restory.UI.Presenters.Inventory;
using Restory.UI.Views.Inventory;
using RestoryMod.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoryMod.Features.PartsBox;

/// <summary>
/// Per-panel state for the parts box sections: remembers the last sort, owns the header objects,
/// swaps the game's grid for <see cref="SectionedGridLayout"/> while sections show, and re-sorts
/// when the workbench changes (placing a part on the table or installing it changes what is
/// missing without touching the box, so the game itself won't refresh).
/// </summary>
internal sealed class PartsBoxSections : MonoBehaviour
{
    private const float PollInterval = 0.3f;

    private static readonly MethodInfo? SortItems = GameTargets.Method(PatchTargets.InventoryPanelSortItems);
    private static readonly MethodInfo? UpdateItemsView = GameTargets.Method(PatchTargets.InventoryPanelUpdateItemsView);
    private static readonly FieldInfo? DeviceServiceField = GameTargets.Field(PatchTargets.InventoryPanelDeviceService);
    private static readonly FieldInfo? ContainerField = GameTargets.Field(PatchTargets.InventoryItemsViewContainer);
    private static readonly FieldInfo? DragServiceField = GameTargets.Field(PatchTargets.InventoryPanelDragService);
    private static readonly FieldInfo? DropServiceField = GameTargets.Field(PatchTargets.InventoryPanelDropService);
    private static readonly FieldInfo? DragStateField = GameTargets.Field(PatchTargets.DragServiceState);
    private static readonly PropertyInfo? DropIsProcess =
        AccessTools.TypeByName(PatchTargets.DropServiceIsProcess.TypeName)?.GetProperty(PatchTargets.DropServiceIsProcess.MemberName!);

    private InventoryPanel panel = null!;
    private string? sortedSignature;
    private int missingCount;
    private float nextPoll;
    private bool layoutLogged;

    private GameObject? missingHeader;
    private GameObject? otherHeader;
    private TMP_Text? cachedStyle;
    private bool styleResolved;

    public static PartsBoxSections For(InventoryPanel panel)
    {
        if (!panel.TryGetComponent<PartsBoxSections>(out var sections))
        {
            sections = panel.gameObject.AddComponent<PartsBoxSections>();
            sections.panel = panel;
        }
        return sections;
    }

    /// <summary>
    /// On unload: these components live on the game's panel and container, so they would outlive the
    /// plugin. Each one's OnDestroy takes its headers; the game's own grid takes over again.
    /// </summary>
    public static void Unload()
    {
        foreach (var layout in FindObjectsByType<SectionedGridLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (layout.TryGetComponent<GridLayoutGroup>(out var grid)) grid.enabled = true;
            Destroy(layout);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)layout.transform);
        }
        foreach (var sections in FindObjectsByType<PartsBoxSections>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(sections);
    }

    /// <summary>
    /// On load: after a hot reload, starts the poll on the live parts box, which re-sorts it into
    /// sections at once if a device is on the workbench. On a normal start there is no panel yet.
    /// </summary>
    public static void AttachToPanels()
    {
        if (SortItems is null || UpdateItemsView is null) return;
        foreach (var panel in FindObjectsByType<InventoryPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            For(panel);
    }

    /// <summary>Called after each sort. A null bench means the game's own order is in effect.</summary>
    public void SetSorted(BenchState? bench, int missing)
    {
        sortedSignature = bench?.Signature;
        missingCount = bench == null ? 0 : missing;
    }

    private void Update()
    {
        if (PartsBoxDiagnostics.Enabled) PartsBoxPerf.Frame();
        if (Time.unscaledTime < nextPoll || !panel || !panel.IsVisible) return;

        // Re-check every frame until the transfer ends; the game's own refresh follows it anyway.
        if (TransferInProgress()) return;
        nextPoll = Time.unscaledTime + PollInterval;

        try
        {
            var deviceService = DeviceServiceField?.GetValue(panel) as DeviceService;
            var signature = PartsBoxFeature.Enabled.Value ? BenchState.Capture(deviceService!)?.Signature : null;
            if (signature == sortedSignature) return;

            SortItems?.Invoke(panel, null);
            UpdateItemsView?.Invoke(panel, null);
        }
        catch (System.Exception e)
        {
            PartsBoxFeature.LogOnce(e);
        }
    }

    /// <summary>
    /// True while a part is being dragged out of / into the box or a drop is animating. Rebuilding
    /// the list then would hand the pooled slot presenter the game's drag holds (draggedSlot) to a
    /// different part, which the game then hides or removes. If the state can't be read (e.g. after a
    /// game update renamed it), report "in progress" so the poll never rebuilds blind.
    /// </summary>
    private bool TransferInProgress()
    {
        if (DragServiceField is null || DragStateField is null || DropServiceField is null || DropIsProcess is null)
            return true;

        var drag = DragServiceField.GetValue(panel);
        if (drag != null && DragStateField.GetValue(drag)?.ToString() != "Disabled") return true;

        var drop = DropServiceField.GetValue(panel);
        return drop != null && DropIsProcess.GetValue(drop) is true;
    }

    /// <summary>
    /// Creates the header objects if needed. Uses Unity's null check, not ??=: a destroyed
    /// GameObject is "fake null" and ??= would keep the dead reference.
    /// </summary>
    private void EnsureHeaders()
    {
        if (!missingHeader) missingHeader = CreateHeader("Missing parts");
        if (!otherHeader) otherHeader = CreateHeader("Other parts");
    }

    /// <summary>One-time work done at scene load instead of on first open.</summary>
    public void Prewarm(InventoryItemsView itemsView, DeviceService deviceService)
    {
        EnsureHeaders();
        if (ContainerField?.GetValue(itemsView) is RectTransform container) HideHeaders(container);
        BenchState.Prewarm(deviceService);
    }

    public void ApplyHeaders(InventoryItemsView itemsView)
    {
        if (ContainerField?.GetValue(itemsView) is not RectTransform container) return;
        var grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null) return;

        var gameChildren = new List<Transform>();
        foreach (Transform child in container)
        {
            if (!child.name.StartsWith(SectionedGridLayout.HeaderPrefix)) gameChildren.Add(child);
        }

        var show = sortedSignature != null && missingCount > 0 && gameChildren.Count > 0;
        if (!container.TryGetComponent<SectionedGridLayout>(out var layout))
        {
            layout = container.gameObject.AddComponent<SectionedGridLayout>();
            if (layout == null)
            {
                PartsBoxFeature.LogOnce(new System.InvalidOperationException("Could not add SectionedGridLayout to the parts box container."));
                return;
            }
            layout.enabled = false;
        }
        var columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? grid.constraintCount : 1;

        HideHeaders(container);
        if (!show)
        {
            layout.enabled = false;
            grid.enabled = true;
            LayoutRebuilder.MarkLayoutForRebuild(container);
            return;
        }

        LogLayoutOnce(grid, columns);
        EnsureHeaders();

        layout.CopyFrom(grid, columns);
        layout.HeaderHeight = HeaderHeight(missingHeader!);
        grid.enabled = false;
        layout.enabled = true;

        var missing = Mathf.Min(missingCount, gameChildren.Count);
        var order = new List<Transform> { ShowHeader(missingHeader!, container) };
        order.AddRange(gameChildren.Take(missing));
        if (missing < gameChildren.Count)
        {
            order.Add(ShowHeader(otherHeader!, container));
            order.AddRange(gameChildren.Skip(missing));
        }

        for (var i = 0; i < order.Count; i++)
            order[i].SetSiblingIndex(i);
        LayoutRebuilder.MarkLayoutForRebuild(container);
    }

    private static Transform ShowHeader(GameObject header, RectTransform container)
    {
        Attach(header.transform, container);
        header.SetActive(true);
        return header.transform;
    }

    /// <summary>
    /// The game detaches every child of the container on rebuild, and Unity keeps world scale when
    /// it does; re-parent our headers with a clean local transform so they don't shrink each time.
    /// </summary>
    private void HideHeaders(RectTransform container)
    {
        foreach (var header in new[] { missingHeader, otherHeader })
        {
            if (!header) continue;
            header!.SetActive(false);
            Attach(header.transform, container);
        }
    }

    private static void Attach(Transform t, RectTransform container)
    {
        t.SetParent(container, false);
        t.localScale = Vector3.one;
        t.localRotation = Quaternion.identity;
        t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, 0);
    }

    private static float HeaderHeight(GameObject header)
    {
        var text = header.GetComponentInChildren<TMP_Text>(true);
        return text != null ? text.fontSize * 1.5f : 60f;
    }

    private GameObject CreateHeader(string label)
    {
        var style = TextStyle();

        var header = new GameObject(SectionedGridLayout.HeaderPrefix + "_" + label.Replace(' ', '_'), typeof(RectTransform));

        // Label stretched over the header row, sitting on a thin rule.
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(header.transform, false);
        var textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 4);
        textRect.offsetMax = Vector2.zero;

        var tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        if (style != null)
        {
            tmp.font = style.font;
            tmp.fontSharedMaterial = style.fontSharedMaterial;
            tmp.fontSize = style.fontSize;
            tmp.color = style.color;
        }

        var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(header.transform, false);
        var ruleRect = (RectTransform)rule.transform;
        ruleRect.anchorMin = Vector2.zero;
        ruleRect.anchorMax = new Vector2(1, 0);
        ruleRect.pivot = new Vector2(0.5f, 0);
        ruleRect.sizeDelta = new Vector2(0, 3);
        var ruleImage = rule.GetComponent<Image>();
        ruleImage.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 0.4f);
        ruleImage.raycastTarget = false;

        header.SetActive(false);
        return header;
    }

    /// <summary>
    /// Borrows font and colour from a label in the panel that uses the game's own localised font
    /// (e.g. the device dropdown), not TMP's LiberationSans fallback.
    /// </summary>
    private TMP_Text? TextStyle()
    {
        if (styleResolved) return cachedStyle;
        styleResolved = true;

        var candidates = panel.GetComponentsInChildren<TMP_Text>(true)
            .Where(t => t.font != null && !t.name.StartsWith(SectionedGridLayout.HeaderPrefix))
            .ToList();
        var style = candidates
            .Where(t => !t.font.name.Contains("Liberation"))
            .OrderByDescending(t => t.GetComponents<MonoBehaviour>().Any(c => c.GetType().Name == "GUI_LocalisedFont"))
            .ThenBy(t => Mathf.Abs(t.fontSize - 42f))
            .FirstOrDefault() ?? candidates.FirstOrDefault();

        if (PartsBoxDiagnostics.Enabled) Plugin.Logger.LogInfo(style == null
            ? "Parts box header: no label to copy style from."
            : $"Parts box header style from '{style.name}' (font {style.font.name}, size {style.fontSize}).");
        cachedStyle = style;
        return style;
    }

    private void LogLayoutOnce(GridLayoutGroup grid, int columns)
    {
        if (layoutLogged || !PartsBoxDiagnostics.Enabled) return;
        layoutLogged = true;
        Plugin.Logger.LogInfo(
            $"Parts box grid: columns={columns}, cell={grid.cellSize}, spacing={grid.spacing}, " +
            $"padding={grid.padding}, alignment={grid.childAlignment}");
    }

    private void OnDestroy()
    {
        if (missingHeader) Destroy(missingHeader);
        if (otherHeader) Destroy(otherHeader);
    }
}
