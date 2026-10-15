using System.Collections.Generic;
using System.Linq;
using Restory.Data.Elements;
using Restory.Data.Elements.Condition;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Quests;
using Restory.Gameplay.Workplace;
using Restory.StorageSystem;
using Restory.StorageSystem.StorageElements;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.Features.PartsBox;

/// <summary>
/// Snapshot of the device on the workbench, as the "How to reassemble" notepad sees it.
/// Mirrors GUI_NotepadElementsPanel / GUI_NotepadElementsPanelView.HandleEmptySocket.
/// </summary>
internal sealed class BenchState
{
    private static WorkSurface? workSurface;

    private BenchState(Device device, Dictionary<ElementInfo, int> ranks, Dictionary<ElementInfo, int> missing, string description)
    {
        Device = device;
        Ranks = ranks;
        Missing = missing;
        Description = description;
    }

    public Device Device { get; }
    public Dictionary<ElementInfo, int> Ranks { get; }

    /// <summary>Number of missing sockets per part type.</summary>
    public Dictionary<ElementInfo, int> Missing { get; }

    /// <summary>Diagnostic summary of the inputs to the missing-parts rule.</summary>
    public string Description { get; }

    /// <summary>Changes whenever the device or its missing sockets change.</summary>
    public string Signature =>
        Device.GetInstanceID() + ":" + string.Join(",",
            Missing.Select(kv => $"{kv.Key.GetInstanceID()}x{kv.Value}").OrderBy(s => s));

    public static BenchState? Capture(DeviceService deviceService)
    {
        var container = deviceService ? deviceService.PlacedDeviceContainer : null;
        var device = container ? container!.Device : null;
        if (device == null) return null;

        var sockets = device.SortedSockets;
        var ranks = PartsBoxOrder.RanksFromSocketOrder(sockets.Select(s => s.CompatibleElementInfo));
        var emptySocketTypes = sockets.Where(s => !s.NestedElement).Select(s => s.CompatibleElementInfo).ToList();
        var looseTypes = LooseParts(container!).Select(e => e.Info).ToList();
        var missing = PartsBoxOrder.FindMissing(emptySocketTypes, looseTypes);

        var description =
            $"device={device.name}; empty sockets=[{Names(emptySocketTypes)}]; loose=[{Names(looseTypes)}]; " +
            $"missing=[{string.Join(", ", missing.Select(kv => $"{NameOf(kv.Key)} x{kv.Value}"))}]";
        return new BenchState(device, ranks, missing, description);
    }

    /// <summary>The box slots that go in the Missing section: one part per missing socket.</summary>
    public HashSet<IReadOnlyStorageSlot> PickMissing(IEnumerable<IReadOnlyStorageSlot> slots)
    {
        var parts = slots.Where(s => s.Item is StorageItemElement).ToList();
        var boxSlots = parts.Select(s =>
        {
            var data = ((StorageItemElement)s.Item).ElementData;
            return new BoxSlot<ElementInfo>(data.Info, ConditionOf(data.Condition), s.Index, s.Count);
        }).ToList();

        return new HashSet<IReadOnlyStorageSlot>(
            PartsBoxOrder.PickMissingSlots(boxSlots, Missing).Select(i => parts[i]));
    }

    public PartsBoxKey KeyFor(StorageItemElement item, int slotIndex, bool missing)
    {
        var data = item.ElementData;
        return new PartsBoxKey(
            missing,
            Ranks.TryGetValue(data.Info, out var rank) ? rank : PartsBoxOrder.NotInDevice,
            ConditionOf(data.Condition),
            slotIndex);
    }

    /// <summary>Finds the workbench and JIT-compiles the hot path ahead of the first open.</summary>
    public static void Prewarm(DeviceService deviceService)
    {
        FindWorkSurface();
        Capture(deviceService);
        PartsBoxOrder.Compare(default, default, false);
        PartsBoxOrder.PickMissingSlots(new[] { new BoxSlot<string>("", PartCondition.Other, 0, 1) },
            PartsBoxOrder.FindMissing(new[] { "" }, new string[0]));
    }

    public static string NameOf(StorageItemElement item) => NameOf(item.ElementData.Info);

    private static string NameOf(ElementInfo info) => info ? info.name : "<null>";

    private static string Names(IEnumerable<ElementInfo> infos) => string.Join(", ", infos.Select(NameOf));

    // Burnt derives from Dirty in the game, so it must be matched first.
    private static PartCondition ConditionOf(ElementConditionBase condition) => condition switch
    {
        BurntElementCondition => PartCondition.Burnt,
        DamagedElementCondition => PartCondition.Damaged,
        DirtyElementCondition => PartCondition.Dirty,
        PerfectElementCondition => PartCondition.Perfect,
        _ => PartCondition.Other,
    };

    private static WorkSurface? FindWorkSurface()
    {
        if (!workSurface) workSurface = Object.FindAnyObjectByType<WorkSurface>();
        return workSurface ? workSurface : null;
    }

    /// <summary>Parts lying loose next to the device, excluding small parts and quest items.</summary>
    private static IEnumerable<ElementBase> LooseParts(DeviceContainer container)
    {
        IEnumerable<ElementBase> parts;
        var parent = container.transform.parent;
        if (parent && parent.TryGetComponent<DismantledDevicePack>(out var pack) && pack.PlacedElements != null)
            parts = pack.PlacedElements.ElementsOnSurface.Select(r => r.Element);
        else
            parts = FindWorkSurface()?.PlacedElements ?? Enumerable.Empty<ElementBase>();

        return parts.Where(e => e && e.Info.Category != ElementCategory.Small && e is not QuestItem);
    }
}
