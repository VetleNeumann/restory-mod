using System.Collections.Generic;
using System.Linq;
using Restory.StorageSystem.StorageElements;
using Restory.UI.Presenters.Controllers;
using Restory.UI.Presenters.Inventory;
using Restory.UI.Presenters.Inventory.StorageSlotElements;
using Restory.UI.Views.Notepad;
using RestoryMod.Features.PartsBox;
using RestoryMod.Features.WorkbenchAdvice;
using Rewired;
using UnityEngine;

namespace RestoryMod.E2E;

/// <summary>
/// Reads what the player sees of the screens over the workbench: the Notepad, the parts box and the
/// controls sign. As <see cref="Bench"/> does for the bench, each read is of the game's own state.
/// </summary>
internal static class Screens
{
    public static string MissingHeader => SectionedGridLayout.HeaderPrefix + "_Missing_parts";
    public static string OtherHeader => SectionedGridLayout.HeaderPrefix + "_Other_parts";

    /// <summary>The Notepad's panel: Hidden, Peeking or Open (CONTEXT.md, Peeking).</summary>
    public static string Notepad() =>
        Object.FindAnyObjectByType<GUI_NotepadWindowView>() is { } view && view ? view.State.ToString() : "no Notepad";

    /// <summary>The inventory panel, which holds the parts box at the workbench.</summary>
    public static InventoryPanel? Inventory() =>
        Object.FindAnyObjectByType<InventoryPanel>() is { } panel && panel ? panel : null;

    public static string InventoryShown() => Inventory() is { } panel ? panel.IsVisible ? "open" : "closed" : "no inventory";

    /// <summary>The parts box list as shown, slots by part name, with the headers among them; empty while closed.</summary>
    public static List<(string row, StorageSlotElement? slot)> BoxRows()
    {
        var rows = new List<(string, StorageSlotElement?)>();
        if (Inventory() is not { IsVisible: true } panel) return rows;
        var slots = panel.GetComponentsInChildren<StorageSlotElement>();
        if (slots.Length == 0) return rows;
        foreach (Transform child in slots[0].transform.parent)
        {
            if (!child.gameObject.activeSelf) continue;
            var slot = child.GetComponent<StorageSlotElement>();
            rows.Add((slot ? PartIn(slot!) : child.name, slot ? slot : null));
        }
        return rows;
    }

    private static string PartIn(StorageSlotElement slot) =>
        slot.Item?.Item is StorageItemElement { Info: Object info } ? Game.NameOf(info) : "(empty slot)";

    /// <summary>Which section of the parts box a part's slot sits in, as text for a check.</summary>
    public static string SectionOf(string part)
    {
        var rows = BoxRows();
        if (rows.Count == 0) return "parts box closed or empty";
        var found = BoxListing.Sections(rows.Select(r => r.row), MissingHeader, OtherHeader)
            .Where(s => s.slot == part).Select(s => s.section.ToString()).FirstOrDefault();
        return found ?? "not in the parts box; there: " + string.Join(", ", rows.Select(r => r.row));
    }

    /// <summary>A part's slot in the open parts box, or a failed step.</summary>
    public static StorageSlotElement Slot(string part) =>
        BoxRows().FirstOrDefault(r => r.row == part).slot is { } slot && slot ? slot
            : throw new StepFailed($"no '{part}' in the open parts box");

    /// <summary>How many of a part the open parts box holds.</summary>
    public static int CountOf(string part) =>
        BoxRows().Where(r => r.row == part && r.slot).Sum(r => r.slot!.Item?.Count ?? 0);

    /// <summary>The rows the mod adds to the workbench controls sign that show now, in order.</summary>
    private static AdviceRows? Sign() =>
        Object.FindAnyObjectByType<AdviceRows>(FindObjectsInactive.Include) is { } rows && rows ? rows : null;

    public static string AdviceShown()
    {
        if (Sign() is not { } rows) return "no controls sign";
        return string.Join(", ", rows.transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("RestoryMod_") && t.gameObject.activeSelf)
            .Select(t => t.name.Substring("RestoryMod_".Length)));
    }

    /// <summary>Whether the sign's zoom-in prompt (the game's R2) shows.</summary>
    public static string ZoomInPrompt()
    {
        if (Sign() is not { } rows) return "no controls sign";
        var zoomIn = rows.GetComponentsInChildren<GUI_InputActionElement>(true).FirstOrDefault(p =>
            p.ActionId == RewiredConsts.Action.CameraControl.Camera_Zoom && p.AxisRange == AxisRange.Positive);
        return !zoomIn ? "no zoom-in prompt" : zoomIn!.gameObject.activeSelf ? "shown" : "hidden";
    }
}
