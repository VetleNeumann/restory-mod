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
/// <summary>What the destinations' rules look at in an object carried away from the workbench.</summary>
/// <param name="Rubbish">Marked by the game as something to throw away, such as an empty box.</param>
/// <param name="HoldsParts">Bought parts, which empty into the parts box.</param>
public readonly record struct CarriedObject(bool Rubbish, bool HoldsParts);

public static class PartSend
{
    /// <summary>
    /// Away from the workbench only the parts box and the trash can (or shredder) take carried
    /// objects; the washing bin and tool cup work on parts at the bench.
    /// </summary>
    public static Destination? CarriedDestinationFor(DpadDirection direction, bool shredderOwned) => direction switch
    {
        DpadDirection.Left => Destination.PartsBox,
        DpadDirection.Down => shredderOwned ? Destination.Shredder : Destination.TrashCan,
        _ => null,
    };

    /// <summary>The game's rules for carried objects: rubbish is thrown away, bought parts are stored.</summary>
    public static bool Accepts(Destination destination, CarriedObject carried) => destination switch
    {
        Destination.TrashCan or Destination.Shredder => carried.Rubbish,
        Destination.PartsBox => carried.HoldsParts,
        _ => false,
    };

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
    /// Grab held on a parts box slot before any drag: the press lifts the part and sends it, so the
    /// shortcut works without first moving the cursor. Left would send it back where it already is.
    /// </summary>
    public static bool SendsFromHeldSlot(DpadDirection direction) => direction != DpadDirection.Left;

    /// <summary>
    /// A part dragged out of the parts box shows as an icon while the cursor is over the box; left then
    /// puts it back in its slot, as letting go there does.
    /// </summary>
    public static bool PutsBackInSlot(DpadDirection direction, bool shownAsIcon) =>
        shownAsIcon && direction == DpadDirection.Left;

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
