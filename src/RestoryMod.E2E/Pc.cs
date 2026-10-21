using System.Linq;
using HarmonyLib;
using Restory.Gameplay.PC;
using Restory.UI.Presenters;
using Restory.UI.Views;
using RestoryMod.Core;
using UnityEngine;

namespace RestoryMod.E2E;

/// <summary>Reads the in-game PC as the player sees it: the app open, the mail selected, the browser tab and category.</summary>
internal static class Pc
{
    private static readonly AccessTools.FieldRef<GUI_MailClient, GUI_MailClientEmailButtonsInFolderPanel> Letters =
        AccessTools.FieldRefAccess<GUI_MailClient, GUI_MailClientEmailButtonsInFolderPanel>(PatchTargets.MailClientList.MemberName);

    private static readonly AccessTools.FieldRef<GUI_MailClient, GUI_MailClientFoldersPanel> Folders =
        AccessTools.FieldRefAccess<GUI_MailClient, GUI_MailClientFoldersPanel>(PatchTargets.MailClientFolders.MemberName);

    private static T Find<T>() where T : Object =>
        Object.FindAnyObjectByType<T>() is { } found && found ? found : throw new StepFailed($"no {typeof(T).Name} in the scene");

    public static string Screen() => Object.FindAnyObjectByType<GUI_PcWindowsXpScreen>(FindObjectsInactive.Include) is { } screen
        && screen && screen.IsVisible ? "shown" : "hidden";

    /// <summary>The app open on the PC by its kind: "GUI_MailClient", "GUI_WebBrowser", or "none".</summary>
    public static string App() => Find<PcAppManager>().LaunchedApp is { } app && app ? app.GetType().Name : "none";

    private static GUI_MailClient Mail() =>
        Find<PcAppManager>().LaunchedApp is GUI_MailClient mail && mail ? mail : throw new StepFailed($"the mail isn't open; open: {App()}");

    private static GUI_WebBrowser Browser() =>
        Find<PcAppManager>().LaunchedApp is GUI_WebBrowser browser && browser ? browser
            : throw new StepFailed($"the browser isn't open; open: {App()}");

    public static string Folder() => Folders(Mail()).CurrentlySelectedFolder.ToString();

    /// <summary>The letter selected in the folder shown, or null.</summary>
    public static object? Letter() => Letters(Mail()).CurrentlySelectedLetter;

    /// <summary>The browser tab shown, by its web address.</summary>
    public static string Tab() => Browser().PageSwitcher.CurrentTab is { } tab && tab ? tab.WebAddress : "none";

    /// <summary>
    /// The category picked in the bar on the shown tab's page, by its place in the bar ("button 2 of 6"), as
    /// the buttons share one name; or "no category bar".
    /// </summary>
    public static string Category()
    {
        var tab = Browser().PageSwitcher.CurrentTab;
        if (!tab || !tab.BrowserPage) return "no page";
        // The game hides the other tabs' bars by making their buttons non-interactable.
        var bar = tab.BrowserPage.GetComponentsInChildren<ToggleButtonGroup>()
            .FirstOrDefault(g => g.Buttons.Any(b => b && b.gameObject.activeInHierarchy && b.IsInteractable()));
        if (!bar) return "no category bar";
        var buttons = bar!.Buttons.Where(b => b).ToList();
        var picked = buttons.FindIndex(b => b.IsSelected);
        return picked < 0 ? "none picked" : $"button {picked + 1} of {buttons.Count}";
    }
}
