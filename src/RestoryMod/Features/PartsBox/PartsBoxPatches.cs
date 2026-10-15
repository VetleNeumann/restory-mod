using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Gameplay.Devices;
using Restory.StorageSystem;
using Restory.StorageSystem.StorageElements;
using Restory.UI.Presenters.Inventory;
using Restory.UI.Views.Inventory;
using RestoryMod.Core;

namespace RestoryMod.Features.PartsBox;

internal static class PartsBoxFeature
{
    public static ConfigEntry<bool> Enabled = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("PartsBox", "Sections", true,
            "While a device is on the workbench, sort the parts box in \"How to reassemble\" order and " +
            "split it into Missing parts / Other parts sections.");
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Parts box sections failed (further errors suppressed): {e}");
    }
}

/// <summary>Re-sorts the box after the game's own sort, while a device is on the workbench.</summary>
[HarmonyPatch]
internal static class PartsBoxSortPatch
{
    private static bool Prepare() =>
        GameTargets.Method(PatchTargets.InventoryPanelSortItems) is not null &&
        GameTargets.Field(PatchTargets.InventoryPanelFilteredItems) is not null &&
        GameTargets.Field(PatchTargets.InventoryPanelFilter) is not null &&
        GameTargets.Field(PatchTargets.InventoryPanelDeviceService) is not null;

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.InventoryPanelSortItems)!;

    private static void Postfix(
        InventoryPanel __instance,
        List<IReadOnlyStorageSlot> ___filteredItems,
        InventoryPanelFilter ___filter,
        DeviceService ___deviceService)
    {
        try
        {
            var sections = PartsBoxSections.For(__instance);
            BenchState? bench;
            using (PartsBoxPerf.Measure("capture"))
                bench = PartsBoxFeature.Enabled.Value ? BenchState.Capture(___deviceService) : null;
            if (bench == null)
            {
                sections.SetSorted(null, 0);
                return;
            }

            var reverse = ___filter.Sort;
            var keys = new Dictionary<IReadOnlyStorageSlot, PartsBoxKey>(___filteredItems.Count);
            foreach (var slot in ___filteredItems)
            {
                if (slot.Item is StorageItemElement item)
                    keys[slot] = bench.KeyFor(item, slot.Index);
            }

            // Non-element items (none expected) keep their place after all parts.
            using (PartsBoxPerf.Measure("order"))
            ___filteredItems.Sort((a, b) =>
            {
                var hasA = keys.TryGetValue(a, out var ka);
                var hasB = keys.TryGetValue(b, out var kb);
                if (!hasA || !hasB) return hasA == hasB ? 0 : hasA ? -1 : 1;
                return PartsBoxOrder.Compare(ka, kb, reverse);
            });

            var missingCount = 0;
            foreach (var slot in ___filteredItems)
            {
                if (!keys.TryGetValue(slot, out var key) || !key.Missing) break;
                missingCount++;
            }

            sections.SetSorted(bench, missingCount);
            using (PartsBoxPerf.Measure("log"))
                LogIfChanged(bench, ___filteredItems, missingCount);
        }
        catch (Exception e)
        {
            PartsBoxFeature.LogOnce(e);
        }
    }

    private static string? lastLogged;

    /// <summary>Logs the inputs and result of the missing-parts rule whenever they change.</summary>
    private static void LogIfChanged(BenchState bench, List<IReadOnlyStorageSlot> sorted, int missingCount)
    {
        if (!PartsBoxPerf.Enabled) return;
        var box = string.Join(", ", sorted.Select((slot, i) =>
            (i < missingCount ? "*" : "") +
            (slot.Item is StorageItemElement item ? BenchState.NameOf(item) : "?") +
            (slot.Count > 1 ? $" x{slot.Count}" : "")));
        var line = $"Parts box: {bench.Description}; box (* = missing section)=[{box}]";
        if (line == lastLogged) return;
        lastLogged = line;
        Plugin.Logger.LogInfo(line);
    }
}

/// <summary>Places the section headers after the game (re)parents the slot views.</summary>
[HarmonyPatch]
internal static class PartsBoxHeadersPatch
{
    private static bool Prepare() =>
        GameTargets.Method(PatchTargets.InventoryPanelUpdateItemsView) is not null &&
        GameTargets.Field(PatchTargets.InventoryPanelView) is not null &&
        GameTargets.Field(PatchTargets.InventoryItemsViewContainer) is not null;

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.InventoryPanelUpdateItemsView)!;

    private static void Postfix(InventoryPanel __instance, InventoryPanelView ___view)
    {
        try
        {
            using (PartsBoxPerf.Measure("headers"))
                PartsBoxSections.For(__instance).ApplyHeaders(___view.Items);
        }
        catch (Exception e)
        {
            PartsBoxFeature.LogOnce(e);
        }
    }
}

/// <summary>
/// Does the parts box's one-time work (header objects, font lookup, workbench lookup, JIT) while
/// the scene loads, so the first open doesn't hitch.
/// </summary>
[HarmonyPatch]
internal static class PartsBoxPrewarmPatch
{
    private static bool Prepare() =>
        GameTargets.Method(PatchTargets.InventoryPanelConstruct) is not null &&
        GameTargets.Field(PatchTargets.InventoryPanelView) is not null;

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.InventoryPanelConstruct)!;

    private static void Postfix(InventoryPanel __instance, InventoryPanelView ___view, DeviceService deviceService)
    {
        try
        {
            using (PartsBoxPerf.Measure("prewarm"))
                PartsBoxSections.For(__instance).Prewarm(___view.Items, deviceService);
            if (PartsBoxPerf.Enabled) PartsBoxPerf.Report("Parts box prewarm at scene load: ");
        }
        catch (Exception e)
        {
            PartsBoxFeature.LogOnce(e);
        }
    }
}
