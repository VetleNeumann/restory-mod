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
using RestoryMod.Features.Fade;
using RestoryMod.Features.MailList;
using Rewired;
using UnityEngine;

namespace RestoryMod.Features.ShoulderButtons;

/// <summary>
/// Reads L1/R1 and L2/R2 each frame and hands the press to whatever is on screen: the Tool window
/// while Cleaning, else the inventory (the parts box at the workbench), else the PC's mail app, else
/// its web browser, else (L1 only) the device at the workbench, which it fades while L1 is held.
/// None of these screens has a per-frame update of its own to hook. The Look direction is handled
/// in <see cref="LookDirectionPatch"/> instead, inside the game's own update for it.
/// </summary>
internal sealed class ShoulderButtonsPoller : MonoBehaviour
{
    private void Update()
    {
        try
        {
            if (!ReInput.isReady) return;
            var player = ReInput.players.GetPlayer(GamePlayer.Id);
            if (player is null) return;

            var bumperStep = Step(player, ShoulderButtonsFeature.PreviousActionId, ShoulderButtonsFeature.NextActionId);
            var triggerStep = Step(player, ShoulderButtonsFeature.LeftActionId, ShoulderButtonsFeature.RightActionId);
            if (bumperStep == 0 && triggerStep == 0) return;
            if (ShoulderButtonsFeature.IsPaused()) return;

            switch (bumperStep == 0 ? BumperTarget.Browser : BumperClaim.For(bumperStep, Screens()))
            {
                case BumperTarget.ToolWindow:
                    StepCleaningTool(bumperStep);
                    return;
                case BumperTarget.Inventory:
                    StepInventory(bumperStep);
                    return;
                case BumperTarget.MailFolders:
                    StepMailFolder(bumperStep);
                    return;
                case BumperTarget.Fade:
                    DeviceFade.Fade();
                    return;
                case BumperTarget.None:
                    bumperStep = 0;
                    break;
            }
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

    /// <summary>Which screens could take an L1/R1 press now: shown, with their feature switched on.</summary>
    private static BumperScreens Screens()
    {
        var window = ShoulderButtonsCapture.ToolWindow;
        var panel = ShoulderButtonsCapture.Inventory;
        var browser = ShoulderButtonsCapture.Browser;
        return new BumperScreens(
            ToolWindow: ShoulderButtonsFeature.CleaningTools.Value && window && ShoulderButtonsCapture.ToolWindowShown(window!),
            Inventory: ShoulderButtonsFeature.Inventory.Value && panel && panel!.IsVisible,
            MailFolders: ShoulderButtonsFeature.MailFolders.Value && ShoulderButtonsCapture.MailFoldersPresent() &&
                         MailApp.Running() is not null,
            Browser: ShoulderButtonsFeature.Browser.Value && browser && browser!.isActiveAndEnabled && browser.IsVisible,
            Fade: DeviceFadeFeature.TargetsPresentCached && (DeviceFade.IsFaded || DeviceFade.Fadeable()));
    }

    /// <summary>Steps the Tool window's slots; L1/R1 belong to it even if nothing changes.</summary>
    private static void StepCleaningTool(int step)
    {
        var window = ShoulderButtonsCapture.ToolWindow!;
        var service = ShoulderButtonsCapture.ToolSelection(window);
        if (!service) return;

        StepOnScreen(window!.CleaningTools,
            slot => slot.ToolInfo == service!.CurrentlySelectedTool,
            slot => slot.gameObject.activeInHierarchy,
            step,
            slot => service!.TryToSelectTool(slot.ToolInfo));
    }

    /// <summary>Steps the inventory's category bar.</summary>
    private static void StepInventory(int step)
    {
        var view = ShoulderButtonsCapture.Inventory!.GetComponentInChildren<InventoryPanelFilterView>();
        if (view) StepCategoryBar(ShoulderButtonsCapture.InventoryCategoryBar(view), step);
    }

    /// <summary>Steps the Mail app's mail folders.</summary>
    private static void StepMailFolder(int step)
    {
        if (MailApp.Running() is not { } client) return;
        var folders = ShoulderButtonsCapture.MailFolders(client);
        if (!folders) return;

        StepOnScreen(ShoulderButtonsCapture.MailFolderButtons(folders!),
            button => button.EmailFolder == folders!.CurrentlySelectedFolder,
            button => button.gameObject.activeInHierarchy,
            step,
            button => ShoulderButtonsCapture.MailFolderButton(button).onClick.Invoke());
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
                tab =>
                {
                    switcher.ResolveTabClick(tab);
                    PcClickSound.Play();
                });
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

    /// <summary>The entries that still exist, in on-screen order; the game's lists don't always run that way.</summary>
    internal static List<T> OnScreen<T>(IEnumerable<T> entries) where T : Component
    {
        var live = entries.Where(e => e).ToList();
        var order = ShoulderStep.ScreenOrder(live.Select(e => (e.transform.position.x, e.transform.position.y)).ToList());
        return order.Select(i => live[i]).ToList();
    }

    /// <summary>
    /// Moves <paramref name="step"/> places from the current entry in on-screen order, wrapping, and
    /// selects that entry.
    /// </summary>
    private static void StepOnScreen<T>(IReadOnlyList<T> entries, Func<T, bool> isCurrent,
        Func<T, bool> isSelectable, int step, Action<T> select) where T : Component
    {
        var onScreen = OnScreen(entries);
        var current = onScreen.FindIndex(e => isCurrent(e));
        var next = ShoulderStep.Cycle(onScreen.Select(isSelectable).ToList(), current, step);
        if (next >= 0 && next != current) select(onScreen[next]);
    }
}
