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

internal sealed class PartsBoxFeature : IFeature
{
    /// <summary>
    /// Checked at runtime rather than in Prepare(), unlike the debug toggles, so the sections can be
    /// switched on and off live from the config file without restarting the game.
    /// </summary>
    public static ConfigEntry<bool> Enabled = null!;

    public FeatureManifest Manifest => FeatureManifests.PartsBox;

    public void Load(FeatureContext ctx)
    {
        Bind(ctx.Config);
        ctx.Patch(typeof(PartsBoxSortPatch), typeof(PartsBoxHeadersPatch), typeof(PartsBoxPrewarmPatch));
        PartsBoxSections.AttachToPanels();
        ctx.OnUnload(PartsBoxSections.Unload);
    }

    private static void Bind(ConfigFile config)
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
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.PartsBox,
        PatchTargets.InventoryPanelSortItems,
        PatchTargets.InventoryPanelFilteredItems,
        PatchTargets.InventoryPanelFilter,
        PatchTargets.InventoryPanelDeviceService);

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

            var missing = bench.PickMissing(___filteredItems);
            var keys = new Dictionary<IReadOnlyStorageSlot, PartsBoxKey>(___filteredItems.Count);
            foreach (var slot in ___filteredItems)
            {
                if (slot.Item is StorageItemElement item)
                    keys[slot] = bench.KeyFor(item, slot.Index, missing.Contains(slot));
            }

            // Non-element items (none expected) keep their place after all parts.
            var reverse = ___filter.Sort;
            using (PartsBoxPerf.Measure("order"))
            ___filteredItems.Sort((a, b) =>
            {
                var hasA = keys.TryGetValue(a, out var ka);
                var hasB = keys.TryGetValue(b, out var kb);
                if (!hasA || !hasB) return hasA == hasB ? 0 : hasA ? -1 : 1;
                return PartsBoxOrder.Compare(ka, kb, reverse);
            });

            sections.SetSorted(bench, missing.Count);
            using (PartsBoxPerf.Measure("log"))
                LogIfChanged(bench, ___filteredItems, missing.Count);
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
        if (!PartsBoxDiagnostics.Enabled) return;
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
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.PartsBox,
        PatchTargets.InventoryPanelUpdateItemsView,
        PatchTargets.InventoryPanelView,
        PatchTargets.InventoryPanelViewItems,
        PatchTargets.InventoryItemsViewContainer);

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
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.PartsBox,
        PatchTargets.InventoryPanelConstruct,
        PatchTargets.InventoryPanelView,
        PatchTargets.InventoryPanelViewItems,
        PatchTargets.InventoryPanelDeviceService);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.InventoryPanelConstruct)!;

    // Construct has just assigned deviceService, so read the field rather than depend on the parameter name.
    private static void Postfix(InventoryPanel __instance, InventoryPanelView ___view, DeviceService ___deviceService)
    {
        try
        {
            using (PartsBoxPerf.Measure("prewarm"))
                PartsBoxSections.For(__instance).Prewarm(___view.Items, ___deviceService);
            if (PartsBoxDiagnostics.Enabled) PartsBoxPerf.Report("Parts box prewarm at scene load: ");
        }
        catch (Exception e)
        {
            PartsBoxFeature.LogOnce(e);
        }
    }
}
