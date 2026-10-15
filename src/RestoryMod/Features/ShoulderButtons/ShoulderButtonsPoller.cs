using System;
using System.Collections.Generic;
using System.Linq;
using Restory.UI.Presenters;
using Restory.UI.Presenters.CleaningToolsSelectionWindow;
using Restory.UI.Presenters.Shops;
using Restory.UI.Views;
using Restory.UI.Views.Inventory;
using Restory.UI.Views.Shops.Devices;
using Restory.UI.Views.Shops.Elements;
using Restory.UI.Views.Shops.HomeDepot;
using RestoryMod.Core;
using Rewired;
using UnityEngine;

namespace RestoryMod.Features.ShoulderButtons;

/// <summary>
/// Reads L1/R1 and L2/R2 each frame and hands the press to whatever is on screen: the Tool window
/// while Cleaning, else the inventory (the parts box at the workbench), else the PC's web browser.
/// None of these screens has a per-frame update of its own to hook. The Look direction is handled
/// in <see cref="LookDirectionPatch"/> instead, inside the game's own update for it.
/// </summary>
internal sealed class ShoulderButtonsPoller : MonoBehaviour
{
    /// <summary>The game reads gamepad input as this Rewired player.</summary>
    private const int GamePlayerId = 0;

    private void Update()
    {
        try
        {
            if (!ReInput.isReady) return;
            var player = ReInput.players.GetPlayer(GamePlayerId);
            if (player is null) return;

            var bumperStep = Step(player, ShoulderButtonsFeature.PreviousActionId, ShoulderButtonsFeature.NextActionId);
            var triggerStep = Step(player, ShoulderButtonsFeature.LeftActionId, ShoulderButtonsFeature.RightActionId);
            if (bumperStep == 0 && triggerStep == 0) return;
            if (ShoulderButtonsFeature.IsPaused()) return;

            if (bumperStep != 0 && (TryStepCleaningTool(bumperStep) || TryStepInventory(bumperStep))) return;
            TryStepBrowser(bumperStep, triggerStep);
        }
        catch (Exception e)
        {
            ShoulderButtonsFeature.LogOnce(e);
        }
    }

    /// <summary>-1 for the left button's press, +1 for the right's, 0 for neither.</summary>
    private static int Step(Player player, int leftActionId, int rightActionId) =>
        player.GetButtonDown(leftActionId) ? -1 : player.GetButtonDown(rightActionId) ? 1 : 0;

    /// <summary>True when the Tool window is shown, so L1/R1 belong to it even if nothing changed.</summary>
    private static bool TryStepCleaningTool(int step)
    {
        if (!ShoulderButtonsFeature.CleaningTools.Value) return false;
        var window = ShoulderButtonsCapture.ToolWindow;
        if (!window || !ShoulderButtonsCapture.ToolWindowShown(window!)) return false;
        var service = ShoulderButtonsCapture.ToolSelection(window!);
        if (!service) return true;

        StepOnScreen(window!.CleaningTools,
            slot => slot.ToolInfo == service!.CurrentlySelectedTool,
            slot => slot.gameObject.activeInHierarchy,
            step,
            slot => service!.TryToSelectTool(slot.ToolInfo));
        return true;
    }

    /// <summary>True when the inventory is shown, so L1/R1 belong to it.</summary>
    private static bool TryStepInventory(int step)
    {
        if (!ShoulderButtonsFeature.Inventory.Value) return false;
        var panel = ShoulderButtonsCapture.Inventory;
        if (!panel || !panel!.IsVisible) return false;

        var view = panel.GetComponentInChildren<InventoryPanelFilterView>();
        if (view) StepCategoryBar(ShoulderButtonsCapture.InventoryCategoryBar(view), step);
        return true;
    }

    private static void TryStepBrowser(int bumperStep, int triggerStep)
    {
        if (!ShoulderButtonsFeature.Browser.Value) return;
        var browser = ShoulderButtonsCapture.Browser;
        if (!browser || !browser!.isActiveAndEnabled || !browser.IsVisible) return;
        var switcher = browser.PageSwitcher;

        if (triggerStep != 0)
        {
            StepOnScreen(switcher.Tabs,
                tab => tab == switcher.CurrentTab,
                tab => tab.gameObject.activeInHierarchy,
                triggerStep,
                switcher.ResolveTabClick);
        }
        else if (switcher.CurrentTab)
        {
            var bar = CategoryBar(switcher.CurrentTab.BrowserPage);
            if (bar) StepCategoryBar(bar!, bumperStep);
        }
    }

    /// <summary>
    /// The category bar on a browser tab's page. The game keeps every tab's page, and the bar behind
    /// the shopping cart, active; it only hides them, so the bar's toggles go non-interactable instead.
    /// </summary>
    private static ToggleButtonGroup? CategoryBar(GUI_WebBrowserPageBase page)
    {
        if (!page) return null;
        var marketplace = page.GetComponentInChildren<GUI_DeviceShopFilterView>();
        if (marketplace) return ShoulderButtonsCapture.MarketplaceCategoryBar(marketplace);
        var homeDepot = page.GetComponentInChildren<GUI_DecorShopProductsPanelFilterView>();
        if (homeDepot) return ShoulderButtonsCapture.HomeDepotCategoryBar(homeDepot);
        var parts = page.GetComponentInChildren<GUI_ElementsShopProductsPanelFilterView>();
        if (parts) return ShoulderButtonsCapture.PartsCategoryBar(parts);
        return null;
    }

    /// <summary>
    /// Clicks the toggle <paramref name="step"/> places along from the selected one, as the player would.
    /// Toggles that are hidden, or can't be clicked (the shopping cart showing, say), are skipped.
    /// </summary>
    private static void StepCategoryBar(ToggleButtonGroup bar, int step) =>
        StepOnScreen(bar.Buttons,
            button => button.IsSelected,
            button => button.gameObject.activeInHierarchy && button.IsInteractable(),
            step,
            button => button.onClick.Invoke());

    /// <summary>
    /// Moves <paramref name="step"/> places from the current entry in on-screen order, wrapping, and
    /// selects that entry. The game's lists don't always run in screen order.
    /// </summary>
    private static void StepOnScreen<T>(IReadOnlyList<T> entries, Func<T, bool> isCurrent,
        Func<T, bool> isSelectable, int step, Action<T> select) where T : Component
    {
        var live = entries.Where(e => e).ToList();
        var order = ShoulderStep.ScreenOrder(live.Select(e => (e.transform.position.x, e.transform.position.y)).ToList());
        var onScreen = order.Select(i => live[i]).ToList();

        var current = onScreen.FindIndex(e => isCurrent(e));
        var next = ShoulderStep.Cycle(onScreen.Select(isSelectable).ToList(), current, step);
        if (next >= 0 && next != current) select(onScreen[next]);
    }
}
