namespace RestoryMod.Core;

/// <summary>Rules for the game's hover outline at the workbench.</summary>
public static class HoverOutline
{
    /// <summary>
    /// Whether the hover outline may show on a part: not on a blocked part, which a press does
    /// nothing to. Some prefabs let the game outline a blocked part in its "can't act" red; the mod
    /// leaves it dark, as most prefabs do.
    /// </summary>
    public static bool Shows(bool blocked, bool switchable) => !blocked || switchable;
}
