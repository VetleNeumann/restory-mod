using System.Collections.Generic;
using System.Linq;
using Restory.Data.Elements;
using Restory.Data.Elements.Condition;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Quests;
using Restory.Gameplay.Workplace;
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

    private BenchState(Device device, Dictionary<ElementInfo, int> ranks, HashSet<ElementInfo> missing)
    {
        Device = device;
        Ranks = ranks;
        Missing = missing;
    }

    public Device Device { get; }
    public Dictionary<ElementInfo, int> Ranks { get; }
    public HashSet<ElementInfo> Missing { get; }

    /// <summary>Changes whenever the device or its set of missing part types changes.</summary>
    public string Signature =>
        Device.GetInstanceID() + ":" + string.Join(",", Missing.Select(i => i.GetInstanceID()).OrderBy(id => id));

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

        return new BenchState(device, ranks, missing)
        {
            Description =
                $"device={device.name}; empty sockets=[{Names(emptySocketTypes)}]; " +
                $"loose=[{Names(looseTypes)}]; missing=[{Names(missing)}]",
        };
    }

    /// <summary>Diagnostic summary of the inputs to the missing-parts rule.</summary>
    public string Description { get; private set; } = "";

    private static string Names(IEnumerable<ElementInfo> infos) =>
        string.Join(", ", infos.Select(i => i ? i.name : "<null>"));

    public static string NameOf(StorageItemElement item) =>
        item.ElementData.Info ? item.ElementData.Info.name : "<null>";

    /// <summary>Finds the workbench and JIT-compiles the hot path ahead of the first open.</summary>
    public static void Prewarm(DeviceService deviceService)
    {
        if (!workSurface) workSurface = Object.FindAnyObjectByType<WorkSurface>();
        Capture(deviceService);
        PartsBoxOrder.Compare(default, default, false);
        PartsBoxOrder.FindMissing(new[] { "" }, new[] { "" });
    }

    public PartsBoxKey KeyFor(StorageItemElement item, int slotIndex)
    {
        var info = item.ElementData.Info;
        return new PartsBoxKey(
            Missing.Contains(info),
            Ranks.TryGetValue(info, out var rank) ? rank : PartsBoxOrder.NotInDevice,
            ConditionOf(item.ElementData.Condition),
            slotIndex);
    }

    private static PartCondition ConditionOf(ElementConditionBase condition) => condition switch
    {
        DamagedElementCondition => PartCondition.Damaged,
        DirtyElementCondition => PartCondition.Dirty,
        PerfectElementCondition => PartCondition.Perfect,
        _ => PartCondition.Other,
    };

    /// <summary>Parts lying loose next to the device, excluding small parts and quest items.</summary>
    private static IEnumerable<ElementBase> LooseParts(DeviceContainer container)
    {
        IEnumerable<ElementBase> parts;
        var parent = container.transform.parent;
        if (parent && parent.TryGetComponent<DismantledDevicePack>(out var pack) && pack.PlacedElements != null)
        {
            parts = pack.PlacedElements.ElementsOnSurface.Select(r => r.Element);
        }
        else
        {
            if (!workSurface) workSurface = Object.FindAnyObjectByType<WorkSurface>();
            parts = workSurface ? workSurface!.PlacedElements : Enumerable.Empty<ElementBase>();
        }

        return parts.Where(e => e && e.Info.Category != ElementCategory.Small && e is not QuestItem);
    }
}
