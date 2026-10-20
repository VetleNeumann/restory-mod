namespace RestoryMod.Core;

/// <summary>What gets an L1/R1 press.</summary>
public enum BumperTarget
{
    None,
    ToolWindow,
    Inventory,
    MailFolders,
    Browser,
    Fade,
}

/// <summary>
/// Which screens could take an L1/R1 press right now (shown, with their feature switched on), and
/// whether the device at the workbench can be Faded.
/// </summary>
public readonly record struct BumperScreens(bool ToolWindow, bool Inventory, bool MailFolders, bool Browser,
    bool Fade);

public static class BumperClaim
{
    /// <summary>
    /// The first screen that claims an L1 (-1) or R1 (+1) press: the Tool window, the inventory, the
    /// mail app, the browser, and last, for L1 only, Fading the device.
    /// </summary>
    public static BumperTarget For(int step, BumperScreens screens)
    {
        if (screens.ToolWindow) return BumperTarget.ToolWindow;
        if (screens.Inventory) return BumperTarget.Inventory;
        if (screens.MailFolders) return BumperTarget.MailFolders;
        if (screens.Browser) return BumperTarget.Browser;
        return step < 0 && screens.Fade ? BumperTarget.Fade : BumperTarget.None;
    }
}
