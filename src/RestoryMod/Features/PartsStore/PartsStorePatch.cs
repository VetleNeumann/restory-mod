using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Restory.Data.Devices;
using Restory.Data.Elements;
using Restory.Data.Shops.Elements;
using Restory.UI.Views.Shops.Elements;
using RestoryMod.Core;

namespace RestoryMod.Features.PartsStore;

internal sealed class PartsStoreFeature : IFeature
{
    /// <summary>Checked at runtime, like the parts box sections, so it can be switched live from the config file.</summary>
    public static ConfigEntry<bool> Enabled = null!;

    public FeatureManifest Manifest => FeatureManifests.PartsStore;

    public void Load(FeatureContext ctx)
    {
        Bind(ctx.Config);
        ctx.Patch(typeof(PartsStoreSortPatch));
        PartsStoreSortPatch.Resume(ctx.Handoff);
        ctx.OnUnload(() => PartsStoreSortPatch.Unload(ctx.Handoff));
    }

    private static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("PartsStore", "ReassemblyOrder", true,
            "Sort the parts store by device model, then in \"How to reassemble\" order instead of by price. " +
            "The store's sort button flips the order within each model.");
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Parts store sort failed (further errors suppressed): {e}");
    }
}

/// <summary>Re-sorts the store's product list after the game's price sort.</summary>
[HarmonyPatch]
internal static class PartsStoreSortPatch
{
    private static bool Prepare() => GameTargets.AllPresent(FeatureManifests.PartsStore);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ShopFilterSortFilteredItems)!;

    /// <summary>
    /// The sort toggle's state when the store first sorted, which shows the notepad's order; the other state
    /// reverses it. The game sets the start state in its prefab, so it is read rather than assumed.
    /// </summary>
    private static GUI_ElementsShopProductsPanelFilterView? startView;
    private static bool startOn;

    private static bool ToggleStart(GUI_ElementsShopProductsPanelFilterView view)
    {
        if (startView != view)
        {
            startView = view;
            startOn = view.IsSortToggleOn;
        }
        return startOn;
    }

    /// <summary>After a hot reload the player may already have flipped the toggle: take the start state over.</summary>
    public static void Resume(Handoff handoff)
    {
        if (handoff.Take() is [GUI_ElementsShopProductsPanelFilterView view, bool on] && view)
            (startView, startOn) = (view, on);
    }

    public static void Unload(Handoff handoff)
    {
        if (startView) handoff.Leave(startView, startOn);
    }

    private static void Postfix(
        List<ElementsShopItemData> ___elementItems,
        List<ElementsShopItemData> ___filteredElementItems,
        GUI_ElementsShopProductsPanelFilterView ___view,
        List<IDeviceInfo> ___devices,
        List<string> ___deviceModels,
        DeviceInfoDatabase ___deviceDatabase)
    {
        try
        {
            // Recorded even while the feature is off, so turning it on live still knows the start state.
            var start = ToggleStart(___view);
            if (!PartsStoreFeature.Enabled.Value) return;

            var selected = ___view.SelectedModelIndex;
            var onlyModel = selected > 0 && selected < ___deviceModels.Count
                ? ___devices.FirstOrDefault(device => device.NameLocalizationKey == ___deviceModels[selected])
                : null;

            var steps = new Dictionary<IDeviceInfo, Dictionary<IElementInfo, int>>();
            ShopKey KeyOf(ElementsShopItemData item)
            {
                var element = item.Element;
                if (!element) return new ShopKey(ShopOrder.NoModel, ShopOrder.NoStep);

                int model;
                IDeviceInfo? device;
                if (onlyModel != null)
                {
                    (model, device) = (0, onlyModel);
                }
                else
                {
                    device = ___deviceDatabase.TryGetDeviceInfo(element, out var assigned) ? assigned : null;
                    model = device == null ? -1 : ___deviceModels.IndexOf(device.NameLocalizationKey);
                    // On a category tab, a part the game assigned to a model outside the tab belongs to the
                    // tab's first model that uses it.
                    if (model < 1)
                        (model, device) = FirstListedModelUsing(element, ___devices, ___deviceModels);
                }
                if (device == null) return new ShopKey(ShopOrder.NoModel, ShopOrder.NoStep);

                if (!steps.TryGetValue(device, out var ranks))
                    steps[device] = ranks = ShopOrder.StepRanks(device.Elements.ToList(), IsStep);
                return new ShopKey(model, ranks.TryGetValue(element, out var step) ? step : ShopOrder.NoStep);
            }

            // The game's price sort is unstable, so ties start from the store's own list order instead.
            var listOrder = new Dictionary<ElementsShopItemData, int>(___elementItems.Count);
            for (var i = 0; i < ___elementItems.Count; i++)
                listOrder.TryAdd(___elementItems[i], i);
            var gameOrder = ___filteredElementItems.OrderBy(item => listOrder.TryGetValue(item, out var i) ? i : int.MaxValue);

            var reverse = ___view.IsSortToggleOn != start;
            var sorted = ShopOrder.Arrange(gameOrder, KeyOf, reverse);
            ___filteredElementItems.Clear();
            ___filteredElementItems.AddRange(sorted);
        }
        catch (Exception e)
        {
            PartsStoreFeature.LogOnce(e);
        }
    }

    /// <summary>The notepad leaves small parts out of "How to reassemble".</summary>
    private static bool IsStep(IElementInfo element) =>
        element is ElementInfo info && info.Category != ElementCategory.Small;

    /// <summary>The dropdown is built in device-list order, so the first device found has the lowest model index.</summary>
    private static (int Model, IDeviceInfo? Device) FirstListedModelUsing(
        IElementInfo element, List<IDeviceInfo> devices, List<string> models)
    {
        foreach (var device in devices)
        {
            var model = models.IndexOf(device.NameLocalizationKey);
            if (model >= 1 && device.Elements.Contains(element))
                return (model, device);
        }
        return (ShopOrder.NoModel, null);
    }
}
