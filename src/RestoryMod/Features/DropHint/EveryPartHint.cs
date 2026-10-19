using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble.StateMachine;
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

    public static bool TargetsPresent() => HintPulse.TargetsPresent() && GameTargets.AllPresent(
        PatchTargets.ElementProjectionData,
        PatchTargets.SocketIsBlocked,
        PatchTargets.SocketNestedElement,
        PatchTargets.SocketCompatibleElementInfo,
        PatchTargets.SocketBlockers,
        PatchTargets.SocketAdditionalBlockers,
        PatchTargets.HintShowDisassembling,
        PatchTargets.HintShowAssembling,
        PatchTargets.HintHighlighter,
        PatchTargets.DeviceElementSockets,
        PatchTargets.SocketIsAvailable,
        PatchTargets.SocketLastNestedElement,
        PatchTargets.ElementDataIsInspected,
        PatchTargets.ElementIsDragging,
        PatchTargets.ElementIsOnSurface,
        PatchTargets.ElementInfoPrefab,
        PatchTargets.FlexibleAttachedModel,
        PatchTargets.FlexiblePlacementModel,
        PatchTargets.BehaviorSwitcherCastCollider,
        PatchTargets.HighlighterPreset,
        PatchTargets.HighlighterFrontOutline,
        PatchTargets.HighlighterBackOutline,
        PatchTargets.TooltipHighlighterComplete);

    private static readonly AccessTools.FieldRef<ElementSocket, HashSet<ElementSocket>> AdditionalBlockers =
        AccessTools.FieldRefAccess<ElementSocket, HashSet<ElementSocket>>("additionalBlockers");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, Device> TargetDevice =
        AccessTools.FieldRefAccess<DisassembleTooltipService, Device>("targetDevice");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, bool> IsDisassembling =
        AccessTools.FieldRefAccess<DisassembleTooltipService, bool>("isDisassembling");
    private static readonly AccessTools.FieldRef<DisassembleTooltipService, DisassembleStateMachine> StateMachine =
        AccessTools.FieldRefAccess<DisassembleTooltipService, DisassembleStateMachine>("disassembleStateMachine");

    private static bool? refreshPresent;

    private static bool RefreshPresent => refreshPresent ??= TargetsPresent() && GameTargets.AllPresent(
        PatchTargets.TooltipServiceTargetDevice,
        PatchTargets.TooltipServiceIsDisassembling,
        PatchTargets.TooltipServiceIsActive,
        PatchTargets.TooltipServiceStateMachine);

    /// <summary>
    /// A ghost's check that also ends it once the game switches between taking apart and putting back
    /// (say a screw goes back in by mistake, then out again).
    /// </summary>
    private static Func<bool> While(bool takingApart, Func<bool> ready) => () =>
    {
        var service = ScrewHintServicePatch.Service;
        var now = service is not null && RefreshPresent ? IsDisassembling(service) : takingApart;
        return now == takingApart && ready();
    };

    /// <summary>
    /// Checks again for ready parts, as the game's hint button would but without its other hints, so
    /// parts that became ready while R2 is held join the pulse.
    /// </summary>
    public static void Refresh(DisassembleTooltipService service, TooltipProjectionHighlighter highlighter)
    {
        if (!RefreshPresent || !service.IsActive || StateMachine(service).ActiveState is not DetectionDisassembleState) return;
        RunGameHint(TargetDevice(service), highlighter, IsDisassembling(service) ? ShowTakingApart : ShowPuttingBack);
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
    public static void ShowTakingApart(Device device, TooltipProjectionHighlighter highlighter)
    {
        var ready = ReadyParts.TakingApart(device.ElementSockets, IsTarget, Blockers,
            socket => socket.NestedElement, socket => socket.IsBlocked);
        foreach (var socket in ready)
        {
            var part = socket.NestedElement;
            ReadyPartGhosts.Add(highlighter, InstalledLook(part), socket.transform,
                While(true, () => socket.NestedElement == part && !socket.IsBlocked));
        }
    }

    /// <summary>
    /// Putting back: screw slots and loose parts that can go in now, and red ghosts in sockets ready
    /// for a part that isn't on the bench.
    /// </summary>
    public static void ShowPuttingBack(Device device, TooltipProjectionHighlighter highlighter)
    {
        var ready = device.ElementSockets.Where(IsReadyToFill).ToList();
        foreach (var slot in ready.Where(s => s.CompatibleElementInfo.Category == ElementCategory.Small))
        {
            var screw = slot.LastNestedElement;
            if (!screw || screw.Info.Category != ElementCategory.Small) continue;
            ReadyPartGhosts.Add(highlighter, screw.ProjectionData, slot.transform, While(false, () => IsReadyToFill(slot)));
        }

        var sockets = ready.Where(s => s.CompatibleElementInfo.Category != ElementCategory.Small).ToList();
        var types = sockets.Select(s => s.CompatibleElementInfo).ToList();
        var loose = BenchState.LooseParts(device).ToList();
        foreach (var part in loose.Where(p => types.Contains(p.Info)))
        {
            ReadyPartGhosts.Add(highlighter, part.ProjectionData, part.transform, While(false, () =>
                part.IsOnSurface && !part.IsDragging && device.ElementSockets.Any(s => s.CompatibleElementInfo == part.Info && IsReadyToFill(s))));
        }

        foreach (var i in ReadyParts.MissingSockets(types, loose.Select(p => p.Info)))
        {
            var socket = sockets[i];
            var type = socket.CompatibleElementInfo;
            ReadyPartGhosts.Add(highlighter, PrefabLook(type), socket.transform,
                While(false, () => IsReadyToFill(socket) && !BenchState.LooseParts(device).Any(p => p.Info == type)), missing: true);
        }
    }

    private static bool IsReadyToFill(ElementSocket socket) =>
        !socket.NestedElement && socket.CompatibleElementInfo && socket.IsAvailable;

    // Screws are never targets: they hold parts, they don't get repaired.
    private static bool IsTarget(ElementSocket socket)
    {
        var part = socket.NestedElement;
        if (part.Info.Category == ElementCategory.Small) return false;
        var data = part.ConditionHandler.ElementData;
        return ReadyParts.IsTarget(data.IsInspected, () => BenchState.ConditionOf(data.Condition));
    }

    // A flip group's lid blocks the part inside through the runtime set, not the serialized list.
    private static IEnumerable<ElementSocket> Blockers(ElementSocket socket) =>
        socket.Blockers.Concat(AdditionalBlockers(socket));

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
    private static void LogOnce(Exception e)
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
