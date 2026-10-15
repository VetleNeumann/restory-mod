using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Equipment;
using Restory.UI.Presenters;
using Restory.UI.Presenters.CleaningToolsSelectionWindow;
using Restory.UI.Presenters.Inventory;
using Restory.UI.Views;
using Restory.UI.Views.Inventory;
using Restory.UI.Views.Shops.Devices;
using Restory.UI.Views.Shops.Elements;
using Restory.UI.Views.Shops.HomeDepot;
using RestoryMod.Core;

namespace RestoryMod.Features.ShoulderButtons;

/// <summary>
/// The screens <see cref="ShoulderButtonsPoller"/> acts on, captured as the game shows them, and the
/// private fields it reads on them. Each screen is captured only when all its fields resolve, so a
/// game update that moves one turns off just that screen's buttons.
/// </summary>
internal static class ShoulderButtonsCapture
{
    public static GUI_CleaningToolsSelectionWindow? ToolWindow;
    public static InventoryPanel? Inventory;
    public static GUI_WebBrowser? Browser;

    private static AccessTools.FieldRef<GUI_CleaningToolsSelectionWindow, bool>? toolWindowShown;
    private static AccessTools.FieldRef<GUI_CleaningToolsSelectionWindow, CleaningToolSelectionService>? toolSelection;
    private static AccessTools.FieldRef<InventoryPanelFilterView, ToggleButtonGroup>? inventoryBar;
    private static AccessTools.FieldRef<GUI_DeviceShopFilterView, ToggleButtonGroup>? marketplaceBar;
    private static AccessTools.FieldRef<GUI_DecorShopProductsPanelFilterView, ToggleButtonGroup>? homeDepotBar;
    private static AccessTools.FieldRef<GUI_ElementsShopProductsPanelFilterView, ToggleButtonGroup>? partsBar;

    public static bool ToolWindowShown(GUI_CleaningToolsSelectionWindow window) =>
        Field(ref toolWindowShown, PatchTargets.ToolWindowIsShown)(window);

    public static CleaningToolSelectionService? ToolSelection(GUI_CleaningToolsSelectionWindow window) =>
        Field(ref toolSelection, PatchTargets.ToolWindowSelectionService)(window);

    public static ToggleButtonGroup InventoryCategoryBar(InventoryPanelFilterView view) =>
        Field(ref inventoryBar, PatchTargets.InventoryFilterCategoryBar)(view);

    public static ToggleButtonGroup MarketplaceCategoryBar(GUI_DeviceShopFilterView view) =>
        Field(ref marketplaceBar, PatchTargets.MarketplaceFilterCategoryBar)(view);

    public static ToggleButtonGroup HomeDepotCategoryBar(GUI_DecorShopProductsPanelFilterView view) =>
        Field(ref homeDepotBar, PatchTargets.HomeDepotFilterCategoryBar)(view);

    public static ToggleButtonGroup PartsCategoryBar(GUI_ElementsShopProductsPanelFilterView view) =>
        Field(ref partsBar, PatchTargets.PartsFilterCategoryBar)(view);

    private static AccessTools.FieldRef<T, F> Field<T, F>(ref AccessTools.FieldRef<T, F>? cached, PatchTarget target) =>
        cached ??= AccessTools.FieldRefAccess<T, F>(target.MemberName!);
}

[HarmonyPatch]
internal static class CleaningToolWindowCapturePatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.ToolWindowShow,
        PatchTargets.ToolWindowIsShown,
        PatchTargets.ToolWindowSelectionService);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.ToolWindowShow)!;

    private static void Postfix(GUI_CleaningToolsSelectionWindow __instance) => ShoulderButtonsCapture.ToolWindow = __instance;
}

[HarmonyPatch]
internal static class InventoryCapturePatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.InventoryPanelShow,
        PatchTargets.InventoryFilterCategoryBar);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.InventoryPanelShow)!;

    private static void Postfix(InventoryPanel __instance) => ShoulderButtonsCapture.Inventory = __instance;
}

[HarmonyPatch]
internal static class BrowserCapturePatch
{
    private static bool Prepare() => GameTargets.AllPresent(
        PatchTargets.BrowserLaunchProcess,
        PatchTargets.MarketplaceFilterCategoryBar,
        PatchTargets.HomeDepotFilterCategoryBar,
        PatchTargets.PartsFilterCategoryBar);

    private static MethodBase TargetMethod() => GameTargets.Method(PatchTargets.BrowserLaunchProcess)!;

    private static void Postfix(GUI_WebBrowser __instance) => ShoulderButtonsCapture.Browser = __instance;
}
