using System;
using BepInEx.Configuration;
using Restory.UI.Presenters.PauseMenu;
using Object = UnityEngine.Object;

namespace RestoryMod.Features.ShoulderButtons;

internal static class ShoulderButtonsFeature
{
    // The game's own UI actions for the gamepad's shoulder buttons and triggers (RewiredConsts.Action.UI);
    // the game defines and binds them but never reads them.
    public const int PreviousActionId = 84; // UI_LB, L1 / LB
    public const int NextActionId = 83;     // UI_RB, R1 / RB
    public const int LeftActionId = 23;     // UI_LT, L2 / LT
    public const int RightActionId = 22;    // UI_RT, R2 / RT

    /// <summary>Each is checked on every press, so they can be switched live from the config file.</summary>
    public static ConfigEntry<bool> LookEnabled = null!;
    public static ConfigEntry<bool> Browser = null!;
    public static ConfigEntry<bool> Inventory = null!;
    public static ConfigEntry<bool> CleaningTools = null!;
    public static ConfigEntry<bool> MailFolders = null!;

    public static void Bind(ConfigFile config)
    {
        LookEnabled = config.Bind("ShoulderButtons", "TriggersLookLeftAndRight", true,
            "Away from the workbench, L2/R2 turn the view one step left or right, as the mouse does at the " +
            "screen edges. At the workbench the triggers keep zooming.");
        Browser = config.Bind("ShoulderButtons", "BrowserTabsAndCategories", true,
            "In the PC's web browser, L2/R2 switch between Marketplace, Home Depot and Parts, and L1/R1 step " +
            "through the shown tab's categories.");
        Inventory = config.Bind("ShoulderButtons", "InventoryCategories", true,
            "In the inventory, L1/R1 step through the device categories.");
        CleaningTools = config.Bind("ShoulderButtons", "CleaningTools", true,
            "While cleaning a part, L1/R1 step through the tools in the Tool window.");
        MailFolders = config.Bind("ShoulderButtons", "MailFolders", true,
            "In the PC's mail app, L1/R1 step through the mail folders.");
    }

    private static GUI_PauseMenu? pauseMenu;

    /// <summary>While the pause menu shows, the game keeps running its screens' updates underneath.</summary>
    public static bool IsPaused()
    {
        if (!pauseMenu) pauseMenu = Object.FindAnyObjectByType<GUI_PauseMenu>();
        return pauseMenu && pauseMenu!.IsShown;
    }

    private static bool errorLogged;

    /// <summary>Logs the first failure only, so a broken target after a game update can't spam the log.</summary>
    public static void LogOnce(Exception e)
    {
        if (errorLogged) return;
        errorLogged = true;
        Plugin.Logger.LogError($"Shoulder buttons failed (further errors suppressed): {e}");
    }
}
