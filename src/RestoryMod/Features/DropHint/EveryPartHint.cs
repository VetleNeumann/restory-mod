using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble.Tooltips;
using Restory.Gameplay.Elements;
using RestoryMod.Core;
using RestoryMod.Features.PartsBox;
using UnityEngine;

namespace RestoryMod.Features.DropHint;

/// <summary>
/// The game's workbench hint (Space, or R2 with the mod) pulses only the next part's screws. With
/// this it pulses every ready part. Taking a device apart: every part or screw that can come out now
/// and is, or stands in the way of, an unknown part or one known to need work. Putting it back: every
/// empty screw slot that can take its screw now, every loose part on the bench whose socket can take
/// it now, and a red ghost in each such socket no loose part can fill. The game still decides which
/// of the two you are doing.
/// </summary>
internal static class EveryPartHintFeature
{
    private const string Section = "Workbench";
    private const string OldKey = "HintShowsEveryScrew";

    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        var definition = new ConfigDefinition(Section, "HintShowsEveryPart");
        var orphans = Orphans(config);
        if (orphans is null) Plugin.Logger.LogWarning($"Can't read old config entries; {OldKey} is not carried over.");
        var carried = orphans is not null && !orphans.ContainsKey(definition)
            && orphans.TryGetValue(new ConfigDefinition(Section, OldKey), out var old) ? old : null;

        Enabled = config.Bind(definition, true, new ConfigDescription(
            "The game's hint at the workbench (Space, or R2 on a gamepad) pulses every part you can act on now. " +
            "Taking a device apart: every part and screw you can take out now on the way to a part you haven't " +
            "seen yet or one that needs cleaning or replacing. Putting it back: every loose part and screw slot " +
            "that can go in now, and a red ghost where a part goes that isn't on the bench."));

        // Renamed from HintShowsEveryScrew: carry its value over once, then drop the old line.
        if (carried is not null)
            Enabled.Value = !string.Equals(carried.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        if (orphans is not null && orphans.Remove(new ConfigDefinition(Section, OldKey))) config.Save();
    }

    /// <summary>The config file's entries no feature has bound (BepInEx keeps them to write back).</summary>
    private static Dictionary<ConfigDefinition, string>? Orphans(ConfigFile config) =>
        AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) as Dictionary<ConfigDefinition, string>;

    public static bool TargetsPresent() => HintPulse.TargetsPresent() &&
        GameTargets.AllPresent(FeatureManifests.EveryPartHint);

    private static readonly AccessTools.FieldRef<ElementSocket, HashSet<ElementSocket>> AdditionalBlockers =
        AccessTools.FieldRefAccess<ElementSocket, HashSet<ElementSocket>>("additionalBlockers");
    private static readonly AccessTools.FieldRef<SerialElementSocket, List<SubordinateElementSocket>> TubePlaces =
        AccessTools.FieldRefAccess<SerialElementSocket, List<SubordinateElementSocket>>("subordinateSockets");
    private static readonly AccessTools.FieldRef<ElementSocket, ElementProjection> SlotMarker =
        AccessTools.FieldRefAccess<ElementSocket, ElementProjection>("smallElementProjection");

    private static bool? refreshPresent;

    private static bool RefreshPresent => refreshPresent ??= TargetsPresent() && WorkbenchSession.StatePresent;

    /// <summary>The sockets and loose parts of the last snapshot, by the spot a ghost on them sits on.</summary>
    private static readonly Dictionary<Transform, ElementSocket> Sockets = new();
    private static readonly Dictionary<Transform, ElementBase> Loose = new();

    /// <summary>
    /// Checks again for ready parts, as the game's hint button would but without its other hints, so
    /// parts that became ready while R2 is held join the pulse.
    /// </summary>
    public static void Refresh(DisassembleTooltipService service, TooltipProjectionHighlighter highlighter)
    {
        if (!RefreshPresent || !WorkbenchSession.Detecting(service)) return;
        var device = WorkbenchSession.Device(service);
        if (!device) return;
        RunGameHint(device!, highlighter, WorkbenchSession.TakingApart(service) ? ShowTakingApart : ShowPuttingBack);
    }

    /// <summary>
    /// The hint as it stands now for the device on the bench, following the game's mode; null when
    /// there's no workbench session to ask.
    /// </summary>
    public static List<HintItem<Transform>>? PlanNow()
    {
        var service = WorkbenchSession.Service;
        if (!Enabled.Value || service is null || !RefreshPresent || !service.IsActive) return null;
        var device = WorkbenchSession.Device(service);
        return device ? Hint.Plan(Snapshot(device!), WorkbenchSession.TakingApart(service)) : null;
    }

    /// <summary>
    /// A prefix's body: ghosts what <paramref name="show"/> picks and returns whether the game's own
    /// hint should run instead (only when this is off or fails).
    /// </summary>
    public static bool RunGameHint(Device device, TooltipProjectionHighlighter highlighter,
        Action<Device, TooltipProjectionHighlighter> show)
    {
        if (!Enabled.Value || !device || !highlighter) return true;
        try
        {
            show(device, highlighter);
            return false;
        }
        catch (Exception e)
        {
            LogOnce(e);
            return true;
        }
    }

    /// <summary>Taking apart: every unblocked part on the way to a part worth taking out.</summary>
    public static void ShowTakingApart(Device device, TooltipProjectionHighlighter highlighter) =>
        Show(device, highlighter, takingApart: true);

    /// <summary>Putting back: ready screw slots and loose parts, and the missing sockets.</summary>
    public static void ShowPuttingBack(Device device, TooltipProjectionHighlighter highlighter) =>
        Show(device, highlighter, takingApart: false);

    private static void Show(Device device, TooltipProjectionHighlighter highlighter, bool takingApart)
    {
        var ledger = HintGhosts.For(highlighter);
        var plan = Hint.Plan(Snapshot(device), takingApart);
        foreach (var item in plan) ledger.Show(item);
        ledger.Reconcile(plan);
    }

    /// <summary>The device's sockets and the loose parts on the bench, as the hint's plan reads them.</summary>
    private static Bench<Transform> Snapshot(Device device)
    {
        Sockets.Clear();
        Loose.Clear();
        var records = new Dictionary<ElementSocket, BenchSocket<Transform>>();
        var unfilled = new Queue<ElementSocket>();
        BenchSocket<Transform> Of(ElementSocket socket)
        {
            if (records.TryGetValue(socket, out var record)) return record;
            var takes = socket.CompatibleElementInfo;
            record = new BenchSocket<Transform>(socket.transform, takes ? takes : null,
                takes && takes.Category == ElementCategory.Small);
            records[socket] = record;
            unfilled.Enqueue(socket);
            Sockets[socket.transform] = socket;
            return record;
        }

        var sockets = device.ElementSockets.Select(Of).ToList();
        // Fill in every socket reached, including blockers and tube places the device doesn't list.
        while (unfilled.Count > 0)
        {
            var socket = unfilled.Dequeue();
            var record = records[socket];
            record.Part = PartOf(socket.NestedElement, installed: true);
            record.LastPart = PartOf(socket.LastNestedElement, installed: false);
            record.Blocked = socket.IsBlocked;
            record.Available = socket.IsAvailable;
            // A flip group's lid blocks the part inside through the runtime set, not the serialized list.
            foreach (var blocker in socket.Blockers.Concat(AdditionalBlockers(socket)))
            {
                if (blocker) record.Blockers.Add(Of(blocker));
            }
            record.IsTubePlace = socket is SubordinateElementSocket;
            if (socket is not SerialElementSocket tube) continue;
            record.TubePlaces = TubePlaces(tube).Select(p => p ? Of(p) : null).ToList();
            record.Closes = tube.BlockedSockets.Select(b => b ? Of(b) : null).ToList();
        }

        var loose = BenchState.LooseParts(device).Where(p => p).Select(p =>
        {
            Loose[p.transform] = p;
            return PartOf(p, installed: false)!;
        }).ToList();
        return new Bench<Transform>(sockets, loose);
    }

    // The condition is read only for an installed part once inspected: the hint never reveals what the
    // game knows about an unknown part, and screws are never repaired.
    private static BenchPart<Transform>? PartOf(ElementBase? part, bool installed)
    {
        if (!part) return null;
        var screw = part!.Info.Category == ElementCategory.Small;
        PartCondition? condition = null;
        if (installed && !screw)
        {
            var data = part.ConditionHandler.ElementData;
            condition = data.IsInspected ? BenchState.ConditionOf(data.Condition) : null;
        }
        return new BenchPart<Transform>(part.transform, part.Info, screw)
        {
            Condition = condition,
            Progress = part.Progress,
            OnSurface = part.IsOnSurface,
            Dragging = part.IsDragging,
        };
    }

    /// <summary>What a ghost of this kind on this spot looks like (from the last snapshot), or null.</summary>
    public static ElementProjectionData? Look(Transform spot, HintKind kind)
    {
        if (kind == HintKind.LoosePart) return Loose.TryGetValue(spot, out var part) && part ? part.ProjectionData : null;
        if (!Sockets.TryGetValue(spot, out var socket) || !socket) return null;
        return kind switch
        {
            HintKind.ScrewSlot => socket.LastNestedElement ? socket.LastNestedElement.ProjectionData : null,
            HintKind.Missing => PrefabLook(socket.CompatibleElementInfo),
            _ => socket.NestedElement ? InstalledLook(socket.NestedElement) : null,
        };
    }

    /// <summary>On unload: lets go of the last snapshot's scene objects.</summary>
    internal static void Unload()
    {
        Sockets.Clear();
        Loose.Clear();
    }

    /// <summary>The game's own marker on a screw slot, or null.</summary>
    public static ElementProjection? Marker(Transform spot)
    {
        if (!Sockets.TryGetValue(spot, out var socket) || !socket) return null;
        var marker = SlotMarker(socket);
        return marker ? marker : null;
    }

    // The same projections the game's detailed hint handler builds.
    private static ElementProjectionData InstalledLook(ElementBase part) =>
        part is FlexibleElement flexible
            ? new ElementProjectionData(flexible.AttachedModel.transform, Vector3.zero, flexible.BehaviorSwitcher.CastCollider)
            : part.ProjectionData;

    private static ElementProjectionData PrefabLook(ElementInfo info)
    {
        var prefab = info.Prefab;
        var model = prefab is FlexibleElement flexible ? flexible.PlacementModel.transform : prefab.transform;
        return new ElementProjectionData(model, Vector3.zero, prefab.BehaviorSwitcher.CastCollider);
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, then lets the game's own hint run.</summary>
    internal static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Every-part hint failed (further errors suppressed): {e}");
    }
}

/// <summary>Taking apart: every ready part instead of the next part's screws.</summary>
[HarmonyPatch]
internal static class EveryPartDisassembleHintPatch
{
    private static bool Prepare() => EveryPartHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.HintShowDisassembling)!;

    private static bool Prefix(Device targetDevice, TooltipProjectionHighlighter ___projectionHighlighter) =>
        EveryPartHintFeature.RunGameHint(targetDevice, ___projectionHighlighter, EveryPartHintFeature.ShowTakingApart);
}

/// <summary>Putting back: every ready screw slot and loose part, and the missing sockets.</summary>
[HarmonyPatch]
internal static class EveryPartAssembleHintPatch
{
    private static bool Prepare() => EveryPartHintFeature.TargetsPresent();

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.HintShowAssembling)!;

    private static bool Prefix(Device targetDevice, TooltipProjectionHighlighter ___projectionHighlighter) =>
        EveryPartHintFeature.RunGameHint(targetDevice, ___projectionHighlighter, EveryPartHintFeature.ShowPuttingBack);
}
