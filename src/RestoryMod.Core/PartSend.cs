namespace RestoryMod.Core;

public enum DpadDirection { Up, Down, Left, Right }

/// <summary>A place at the workbench that takes a held part away from the bench in one move.</summary>
public enum Destination { PartsBox, WashingBin, ToolCup, TrashCan, Shredder }

/// <summary>What the destinations' rules look at in a held part.</summary>
/// <param name="NeedsCleaning">Dirty or in need of soldering: what the tool cup works on.</param>
public readonly record struct HeldPart(bool Damaged, bool QuestItem, bool NeedsCleaning);

/// <summary>
/// Rules for sending a held part to a destination with one D-pad press. A send the destination would
/// refuse does nothing, so these mirror the game's own drop rules: checked before the drop, a refusal
/// never reaches the game's fallback (bench surface or a nearby socket).
/// </summary>
public static class PartSend
{
    /// <summary>
    /// Up, right and left point roughly where the washing bin, tool cup and parts box sit on screen;
    /// down throws away. The shredder, once bought, stands in the trash can's place.
    /// </summary>
    public static Destination DestinationFor(DpadDirection direction, bool shredderOwned) => direction switch
    {
        DpadDirection.Up => Destination.WashingBin,
        DpadDirection.Right => Destination.ToolCup,
        DpadDirection.Left => Destination.PartsBox,
        _ => shredderOwned ? Destination.Shredder : Destination.TrashCan,
    };

    /// <summary>
    /// The part-based half of the game's rules. The washing bin's own state (unlocked, full, running)
    /// is left to the game, which checks it when the part is fitted in.
    /// </summary>
    public static bool Accepts(Destination destination, HeldPart part) => destination switch
    {
        Destination.TrashCan or Destination.Shredder => part.Damaged,
        Destination.ToolCup => !part.Damaged && part.NeedsCleaning,
        _ => !part.Damaged && !part.QuestItem,
    };
}
